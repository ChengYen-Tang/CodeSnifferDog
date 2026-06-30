using Bunit;
using AngleSharp.Dom;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using System.Reflection;
using HomePage = CodeSnifferDog.Server.Client.Pages.Home;

namespace CodeSnifferDog.Tests.Components.Pages;

[TestClass]
public sealed class HomeTests
{
    [TestMethod]
    public void InitialRender_ShowsEmptyArchiveSelectionAndDisabledUpload()
    {
        using Bunit.TestContext context = new();
        IRenderedComponent<HomePage> cut = RenderHome(context);

        Assert.AreEqual("No archive selected", SelectedArchiveInput(cut).GetAttribute("value"));
        Assert.IsTrue(UploadButton(cut).HasAttribute("disabled"));
        StringAssert.Contains(cut.Markup, "New Project");
        StringAssert.Contains(cut.Markup, "Drop archive here or click to browse");
        StringAssert.Contains(cut.Markup, ".zip only");
    }

    [TestMethod]
    public void ZipFileSelection_ShowsSelectedArchiveAndEnablesUpload()
    {
        using Bunit.TestContext context = new();
        IRenderedComponent<HomePage> cut = RenderHome(context);

        UploadFile(cut, InputFileContent.CreateFromText("zip-content", "repo.zip", contentType: "application/zip"));

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("repo.zip (11 B)", SelectedArchiveInput(cut).GetAttribute("value"));
            Assert.IsFalse(UploadButton(cut).HasAttribute("disabled"));
            StringAssert.Contains(cut.Markup, "repo.zip (11 B)");
        });
    }

    [TestMethod]
    public void NonZipFileSelection_ShowsValidationAndKeepsUploadDisabled()
    {
        using Bunit.TestContext context = new();
        IRenderedComponent<HomePage> cut = RenderHome(context);

        UploadFile(cut, InputFileContent.CreateFromText("not-a-zip", "repo.txt", contentType: "text/plain"));

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("No archive selected", SelectedArchiveInput(cut).GetAttribute("value"));
            Assert.IsTrue(UploadButton(cut).HasAttribute("disabled"));
            StringAssert.Contains(cut.Find(".alert").TextContent, "Only .zip uploads are supported.");
        });
    }

    [TestMethod]
    public void UploadWithoutSelectedFile_ShowsWarning()
    {
        using Bunit.TestContext context = new();
        IRenderedComponent<HomePage> cut = RenderHome(context);

        InvokeUploadProject(cut);

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Find(".alert").TextContent, "Choose a .zip archive before uploading.");
        });
    }

    [TestMethod]
    public void UploadSuccess_NavigatesToAgentStatus()
    {
        using Bunit.TestContext context = new();
        Guid projectId = Guid.Parse("9a000000-0000-0000-0000-000000000001");
        FakeProjectUploadJsRuntime jsRuntime = RegisterUploadJsRuntime(context, ProjectUploadJsResult.ForSuccess(projectId));
        NavigationManager navigationManager = context.Services.GetRequiredService<NavigationManager>();
        IRenderedComponent<HomePage> cut = RenderHome(context);
        UploadFile(cut, InputFileContent.CreateFromText("zip-content", "repo.zip", contentType: "application/zip"));

        UploadButton(cut).Click();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual("codeSnifferDogProjectUpload.upload", jsRuntime.LastIdentifier);
            CollectionAssert.AreEqual(new object?[] { "new-project-zip-input", "/api/projects/" }, jsRuntime.LastArguments);
            Assert.AreEqual($"http://localhost/agent-status?projectId={projectId}", navigationManager.Uri);
        });
    }

    [TestMethod]
    public void UploadFailure_ShowsServerMessage()
    {
        using Bunit.TestContext context = new();
        RegisterUploadJsRuntime(context, ProjectUploadJsResult.Failure("Archive could not be read."));
        IRenderedComponent<HomePage> cut = RenderHome(context);
        UploadFile(cut, InputFileContent.CreateFromText("zip-content", "repo.zip", contentType: "application/zip"));

        UploadButton(cut).Click();

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Find(".alert").TextContent, "Archive could not be read.");
            Assert.IsFalse(UploadButton(cut).HasAttribute("disabled"));
        });
    }

    [TestMethod]
    public void UploadNullResponse_ShowsEmptyResponseMessage()
    {
        using Bunit.TestContext context = new();
        RegisterUploadJsRuntime(context, ProjectUploadJsResult.Null());
        IRenderedComponent<HomePage> cut = RenderHome(context);
        UploadFile(cut, InputFileContent.CreateFromText("zip-content", "repo.zip", contentType: "application/zip"));

        UploadButton(cut).Click();

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Find(".alert").TextContent, "The upload completed, but the server response was empty.");
            Assert.IsFalse(UploadButton(cut).HasAttribute("disabled"));
        });
    }

    private static IRenderedComponent<HomePage> RenderHome(Bunit.TestContext context) =>
        context.RenderComponent<HomePage>();

    private static IElement SelectedArchiveInput(IRenderedComponent<HomePage> cut) =>
        cut.Find("input.form-control[readonly]");

    private static IElement UploadButton(IRenderedComponent<HomePage> cut) =>
        cut.Find(".upload-action-button");

    private static void UploadFile(IRenderedComponent<HomePage> cut, InputFileContent file) =>
        cut.FindComponent<InputFile>().UploadFiles(file);

    private static void InvokeUploadProject(IRenderedComponent<HomePage> cut)
    {
        cut.InvokeAsync(() =>
        {
            MethodInfo? method = typeof(HomePage).GetMethod("UploadProjectAsync", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            return (Task)(method.Invoke(cut.Instance, []) ?? Task.CompletedTask);
        }).GetAwaiter().GetResult();
        cut.Render();
    }

    private static FakeProjectUploadJsRuntime RegisterUploadJsRuntime(Bunit.TestContext context, ProjectUploadJsResult result)
    {
        FakeProjectUploadJsRuntime jsRuntime = new(result);
        context.Services.AddSingleton<IJSRuntime>(jsRuntime);
        return jsRuntime;
    }

    private sealed class FakeProjectUploadJsRuntime(ProjectUploadJsResult result) : IJSRuntime
    {
        private readonly ProjectUploadJsResult _result = result;

        public string? LastIdentifier { get; private set; }
        public object?[]? LastArguments { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            LastIdentifier = identifier;
            LastArguments = args;
            return ValueTask.FromResult(CreateResponse<TValue>());
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);

        private TValue CreateResponse<TValue>()
        {
            if (typeof(TValue).FullName == "Microsoft.JSInterop.Infrastructure.IJSVoidResult")
                return default!;

            if (_result.ReturnNull)
                return default!;

            Type responseType = typeof(TValue);
            object response = Activator.CreateInstance(responseType, nonPublic: true)
                ?? throw new InvalidOperationException($"Unable to create upload response '{responseType}'.");

            SetProperty(response, "Success", _result.Success);
            SetProperty(response, "Message", _result.Message);
            SetProperty(response, "ProjectId", _result.ProjectId);
            SetProperty(response, "OriginalFileName", "repo.zip");
            return (TValue)response;
        }

        private static void SetProperty(object instance, string propertyName, object? value)
        {
            PropertyInfo? property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(property);
            property.SetValue(instance, value);
        }
    }

    private sealed record ProjectUploadJsResult(
        bool Success,
        string? Message,
        Guid ProjectId,
        bool ReturnNull)
    {
        public static ProjectUploadJsResult ForSuccess(Guid projectId) => new(true, null, projectId, false);

        public static ProjectUploadJsResult Failure(string message) => new(false, message, Guid.Empty, false);

        public static ProjectUploadJsResult Null() => new(false, null, Guid.Empty, true);
    }
}
