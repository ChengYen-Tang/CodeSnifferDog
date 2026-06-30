using Bunit;
using AngleSharp.Dom;
using CodeSnifferDog.Server.Shared.Reports;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using ReportsPage = CodeSnifferDog.Server.Client.Pages.Reports;

namespace CodeSnifferDog.Tests.Components.Pages;

[TestClass]
public sealed class ReportsTests
{
    [TestMethod]
    public void LoadsReportListThenFetchesOnlySelectedReportContent()
    {
        using Bunit.TestContext context = new();
        ConfigureServices(context);
        ReportApiMessageHandler handler = new(
            new ProjectReportListDto
            {
                OriginalFileName = "demo.zip",
                Reports =
                [
                    new ProjectReportListItemDto
                    {
                        ReportId = Guid.Parse("81000000-0000-0000-0000-000000000001"),
                        RuleName = "rule-a",
                    },
                    new ProjectReportListItemDto
                    {
                        ReportId = Guid.Parse("81000000-0000-0000-0000-000000000002"),
                        RuleName = "rule-b",
                    }
                ],
            },
            new Dictionary<Guid, ProjectReportContentDto>
            {
                [Guid.Parse("81000000-0000-0000-0000-000000000001")] = new()
                {
                    ReportId = Guid.Parse("81000000-0000-0000-0000-000000000001"),
                    RuleName = "rule-a",
                    MarkdownContent = "# Rule A\n\nAlpha content",
                },
                [Guid.Parse("81000000-0000-0000-0000-000000000002")] = new()
                {
                    ReportId = Guid.Parse("81000000-0000-0000-0000-000000000002"),
                    RuleName = "rule-b",
                    MarkdownContent = "# Rule B\n\nBeta content",
                },
            });

        context.Services.AddSingleton(new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost"),
        });

        NavigationManager navigationManager = context.Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo("http://localhost/reports/80000000-0000-0000-0000-000000000001");

        IRenderedComponent<ReportsPage> cut = context.RenderComponent<ReportsPage>(
            parameters => parameters.Add(component => component.ProjectId, Guid.Parse("80000000-0000-0000-0000-000000000001")));

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "rule-a.md");
            StringAssert.Contains(cut.Markup, "Alpha content");
            Assert.DoesNotContain(cut.Markup, "Beta content");
        });

        CollectionAssert.AreEqual(
            new[]
            {
                "/api/projects/80000000-0000-0000-0000-000000000001/reports",
                "/api/projects/80000000-0000-0000-0000-000000000001/reports/81000000-0000-0000-0000-000000000001"
            },
            handler.Requests.ToArray());

        IElement secondReportButton = cut.FindAll(".report-file-select")[1];
        secondReportButton.Click();

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "rule-b.md");
            StringAssert.Contains(cut.Markup, "Beta content");
            Assert.DoesNotContain(cut.Markup, "Alpha content");
        });

        CollectionAssert.AreEqual(
            new[]
            {
                "/api/projects/80000000-0000-0000-0000-000000000001/reports",
                "/api/projects/80000000-0000-0000-0000-000000000001/reports/81000000-0000-0000-0000-000000000001",
                "/api/projects/80000000-0000-0000-0000-000000000001/reports/81000000-0000-0000-0000-000000000002"
            },
            handler.Requests.ToArray());
    }

    [TestMethod]
    public void KeepsLatestSelectedReportWhenEarlierRequestFinishesLater()
    {
        using Bunit.TestContext context = new();
        ConfigureServices(context);
        SequencedReportApiMessageHandler handler = new(
            new ProjectReportListDto
            {
                OriginalFileName = "demo.zip",
                Reports =
                [
                    new ProjectReportListItemDto
                    {
                        ReportId = Guid.Parse("81000000-0000-0000-0000-000000000011"),
                        RuleName = "rule-a",
                    },
                    new ProjectReportListItemDto
                    {
                        ReportId = Guid.Parse("81000000-0000-0000-0000-000000000012"),
                        RuleName = "rule-b",
                    }
                ],
            },
            [
                SequencedResponse.ForContent(
                    Guid.Parse("81000000-0000-0000-0000-000000000011"),
                    new ProjectReportContentDto
                    {
                        ReportId = Guid.Parse("81000000-0000-0000-0000-000000000011"),
                        RuleName = "rule-a",
                        MarkdownContent = "# Rule A\n\nInitial alpha",
                    }),
                SequencedResponse.ForDelayedContent(
                    Guid.Parse("81000000-0000-0000-0000-000000000012"),
                    new ProjectReportContentDto
                    {
                        ReportId = Guid.Parse("81000000-0000-0000-0000-000000000012"),
                        RuleName = "rule-b",
                        MarkdownContent = "# Rule B\n\nDelayed beta",
                    }),
                SequencedResponse.ForContent(
                    Guid.Parse("81000000-0000-0000-0000-000000000011"),
                    new ProjectReportContentDto
                    {
                        ReportId = Guid.Parse("81000000-0000-0000-0000-000000000011"),
                        RuleName = "rule-a",
                        MarkdownContent = "# Rule A\n\nReloaded alpha",
                    }),
            ]);

        context.Services.AddSingleton(new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost"),
        });

        IRenderedComponent<ReportsPage> cut = context.RenderComponent<ReportsPage>(
            parameters => parameters.Add(component => component.ProjectId, Guid.Parse("80000000-0000-0000-0000-000000000011")));

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "Initial alpha");
        });

        cut.FindAll(".report-file-select")[1].Click();
        cut.FindAll(".report-file-select")[0].Click();

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "Reloaded alpha");
            Assert.DoesNotContain(cut.Markup, "Delayed beta");
        });

        handler.ReleaseNextDelayedResponse();

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "Reloaded alpha");
            Assert.DoesNotContain(cut.Markup, "Delayed beta");
            StringAssert.Contains(cut.Find(".report-file-item.active").TextContent, "rule-a");
        });
    }

    [TestMethod]
    public void AllowsRetryWhenInitialSelectedReportFailsToLoad()
    {
        using Bunit.TestContext context = new();
        ConfigureServices(context);
        SequencedReportApiMessageHandler handler = new(
            new ProjectReportListDto
            {
                OriginalFileName = "demo.zip",
                Reports =
                [
                    new ProjectReportListItemDto
                    {
                        ReportId = Guid.Parse("81000000-0000-0000-0000-000000000021"),
                        RuleName = "rule-a",
                    }
                ],
            },
            [
                SequencedResponse.ForFailure(Guid.Parse("81000000-0000-0000-0000-000000000021"), HttpStatusCode.InternalServerError),
                SequencedResponse.ForContent(
                    Guid.Parse("81000000-0000-0000-0000-000000000021"),
                    new ProjectReportContentDto
                    {
                        ReportId = Guid.Parse("81000000-0000-0000-0000-000000000021"),
                        RuleName = "rule-a",
                        MarkdownContent = "# Rule A\n\nRecovered alpha",
                    }),
            ]);

        context.Services.AddSingleton(new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost"),
        });

        IRenderedComponent<ReportsPage> cut = context.RenderComponent<ReportsPage>(
            parameters => parameters.Add(component => component.ProjectId, Guid.Parse("80000000-0000-0000-0000-000000000021")));

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "Failed to load report content:");
            StringAssert.Contains(cut.Markup, "500");
        });

        IElement firstButton = cut.Find(".report-file-select");
        firstButton.Click();

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "Recovered alpha");
            Assert.DoesNotContain(cut.Markup, "Failed to load report content:");
        });

        CollectionAssert.AreEqual(
            new[]
            {
                "/api/projects/80000000-0000-0000-0000-000000000021/reports",
                "/api/projects/80000000-0000-0000-0000-000000000021/reports/81000000-0000-0000-0000-000000000021",
                "/api/projects/80000000-0000-0000-0000-000000000021/reports/81000000-0000-0000-0000-000000000021"
            },
            handler.Requests.ToArray());
    }

    [TestMethod]
    public void LargeReportList_RendersListAndSelectedContent()
    {
        using Bunit.TestContext context = new();
        ConfigureServices(context);
        Guid projectId = Guid.Parse("80000000-0000-0000-0000-000000000100");
        Guid firstReportId = Guid.Parse("81000000-0000-0000-0000-000000000100");
        ProjectReportListDto reportList = new()
        {
            OriginalFileName = "large-demo.zip",
            Reports = Enumerable.Range(1, 200)
                .Select(index => new ProjectReportListItemDto
                {
                    ReportId = index == 1
                        ? firstReportId
                        : Guid.Parse($"81000000-0000-0000-0001-{index:000000000000}"),
                    RuleName = $"rule-{index:000}",
                })
                .ToList(),
        };
        SequencedReportApiMessageHandler handler = new(
            reportList,
            [
                SequencedResponse.ForContent(
                    firstReportId,
                    new ProjectReportContentDto
                    {
                        ReportId = firstReportId,
                        RuleName = "rule-001",
                        MarkdownContent = "# Rule 001\n\nLarge list first content",
                    })
            ]);
        context.Services.AddSingleton(new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost"),
        });

        IRenderedComponent<ReportsPage> cut = context.RenderComponent<ReportsPage>(
            parameters => parameters.Add(component => component.ProjectId, projectId));

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual(200, cut.FindAll(".report-file-item").Count);
            Assert.AreEqual(1, cut.FindAll(".report-file-item.active").Count);
            StringAssert.Contains(cut.Markup, "rule-200.md");
            StringAssert.Contains(cut.Markup, "Large list first content");
        });
    }

    [TestMethod]
    public void SelectedReportMarkdown_IsCachedAcrossRender()
    {
        using Bunit.TestContext context = new();
        ConfigureServices(context);
        Guid projectId = Guid.Parse("80000000-0000-0000-0000-000000000101");
        Guid reportId = Guid.Parse("81000000-0000-0000-0000-000000000101");
        SequencedReportApiMessageHandler handler = new(
            new ProjectReportListDto
            {
                OriginalFileName = "demo.zip",
                Reports =
                [
                    new ProjectReportListItemDto
                    {
                        ReportId = reportId,
                        RuleName = "rule-cache",
                    }
                ],
            },
            [
                SequencedResponse.ForContent(
                    reportId,
                    new ProjectReportContentDto
                    {
                        ReportId = reportId,
                        RuleName = "rule-cache",
                        MarkdownContent = "# Rule Cache\n\nCached content",
                    })
            ]);
        context.Services.AddSingleton(new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost"),
        });

        IRenderedComponent<ReportsPage> cut = context.RenderComponent<ReportsPage>(
            parameters => parameters.Add(component => component.ProjectId, projectId));

        cut.WaitForAssertion(() => StringAssert.Contains(cut.Markup, "Cached content"));
        MarkupString firstMarkup = GetSelectedReportMarkup(cut);

        cut.Render();
        cut.Render();

        MarkupString secondMarkup = GetSelectedReportMarkup(cut);
        Assert.AreEqual(firstMarkup.Value, secondMarkup.Value);
        CollectionAssert.AreEqual(
            new[]
            {
                $"/api/projects/{projectId}/reports",
                $"/api/projects/{projectId}/reports/{reportId}"
            },
            handler.Requests.ToArray());
    }

    private static void ConfigureServices(Bunit.TestContext context)
    {
        ConstructorInfo constructor = typeof(PersistentComponentState)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single();
        ParameterInfo[] parameters = constructor.GetParameters();

        object persistentComponentState = constructor.Invoke(
            [
                new Dictionary<string, byte[]>(),
                CreateEmptyList(parameters[1].ParameterType),
                CreateEmptyList(parameters[2].ParameterType),
            ]);

        context.Services.AddSingleton((PersistentComponentState)persistentComponentState);
    }

    private static object CreateEmptyList(Type listType) =>
        Activator.CreateInstance(listType)
        ?? throw new InvalidOperationException($"Unable to create instance of {listType}.");

    private static MarkupString GetSelectedReportMarkup(IRenderedComponent<ReportsPage> cut)
    {
        FieldInfo? field = typeof(ReportsPage).GetField("_selectedReportMarkup", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        return (MarkupString)(field.GetValue(cut.Instance) ?? default(MarkupString));
    }

    private sealed class ReportApiMessageHandler(
        ProjectReportListDto list,
        IReadOnlyDictionary<Guid, ProjectReportContentDto> contents) : HttpMessageHandler
    {
        private readonly ProjectReportListDto _list = list;
        private readonly IReadOnlyDictionary<Guid, ProjectReportContentDto> _contents = contents;

        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string path = request.RequestUri?.AbsolutePath ?? string.Empty;
            Requests.Add(path);

            object? payload = path.EndsWith("/reports", StringComparison.Ordinal)
                ? _list
                : _contents.FirstOrDefault(pair => path.EndsWith($"/{pair.Key}", StringComparison.Ordinal)).Value;

            if (payload is null)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload)),
            });
        }
    }

    private sealed class SequencedReportApiMessageHandler(
        ProjectReportListDto list,
        IReadOnlyList<SequencedResponse> responses) : HttpMessageHandler
    {
        private readonly ProjectReportListDto _list = list;
        private readonly Queue<SequencedResponse> _responses = new(responses);
        private readonly Queue<(TaskCompletionSource<HttpResponseMessage> Tcs, SequencedResponse Response)> _delayedResponses = [];

        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string path = request.RequestUri?.AbsolutePath ?? string.Empty;
            Requests.Add(path);

            if (path.EndsWith("/reports", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(_list)),
                });
            }

            Assert.IsTrue(_responses.Count > 0, $"Unexpected report content request: {path}");
            SequencedResponse response = _responses.Dequeue();
            Assert.IsTrue(path.EndsWith($"/{response.ReportId}", StringComparison.Ordinal), $"Unexpected report request order: {path}");

            if (response.DelayCompletion)
            {
                TaskCompletionSource<HttpResponseMessage> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
                _delayedResponses.Enqueue((tcs, response));
                return tcs.Task;
            }

            return Task.FromResult(CreateResponseMessage(response));
        }

        public void ReleaseNextDelayedResponse()
        {
            Assert.IsTrue(_delayedResponses.Count > 0, "No delayed response is pending.");
            (TaskCompletionSource<HttpResponseMessage> tcs, SequencedResponse response) = _delayedResponses.Dequeue();
            tcs.SetResult(CreateResponseMessage(response));
        }

        private static HttpResponseMessage CreateResponseMessage(SequencedResponse response)
        {
            if (response.StatusCode != HttpStatusCode.OK)
                return new HttpResponseMessage(response.StatusCode);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(response.Content)),
            };
        }
    }

    private sealed record SequencedResponse(
        Guid ReportId,
        HttpStatusCode StatusCode,
        ProjectReportContentDto? Content,
        bool DelayCompletion)
    {
        public static SequencedResponse ForContent(Guid reportId, ProjectReportContentDto content) =>
            new(reportId, HttpStatusCode.OK, content, false);

        public static SequencedResponse ForDelayedContent(Guid reportId, ProjectReportContentDto content) =>
            new(reportId, HttpStatusCode.OK, content, true);

        public static SequencedResponse ForFailure(Guid reportId, HttpStatusCode statusCode) =>
            new(reportId, statusCode, null, false);
    }
}
