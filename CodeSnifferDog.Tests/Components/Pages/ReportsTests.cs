using Bunit;
using AngleSharp.Dom;
using CodeSnifferDog.Server.Client.Pages;
using CodeSnifferDog.Server.Shared.Reports;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace CodeSnifferDog.Tests.Components.Pages;

[TestClass]
public sealed class ReportsTests
{
    [TestMethod]
    public void LoadsReportListThenFetchesOnlySelectedReportContent()
    {
        using Bunit.TestContext context = new();
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

        IRenderedComponent<Reports> cut = context.RenderComponent<Reports>(
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

        IRenderedComponent<Reports> cut = context.RenderComponent<Reports>(
            parameters => parameters.Add(component => component.ProjectId, Guid.Parse("80000000-0000-0000-0000-000000000011")));

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "Initial alpha");
        });

        IElement firstButton = cut.FindAll(".report-file-select")[0];
        IElement secondButton = cut.FindAll(".report-file-select")[1];

        secondButton.Click();
        firstButton.Click();

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

        IRenderedComponent<Reports> cut = context.RenderComponent<Reports>(
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

            object? payload = path switch
            {
                "/api/projects/80000000-0000-0000-0000-000000000001/reports" => _list,
                "/api/projects/80000000-0000-0000-0000-000000000001/reports/81000000-0000-0000-0000-000000000001" => _contents[Guid.Parse("81000000-0000-0000-0000-000000000001")],
                "/api/projects/80000000-0000-0000-0000-000000000001/reports/81000000-0000-0000-0000-000000000002" => _contents[Guid.Parse("81000000-0000-0000-0000-000000000002")],
                _ => null
            };

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
