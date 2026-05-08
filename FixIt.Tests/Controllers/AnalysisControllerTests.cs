using FixIt.Controllers;
using FixIt.Services.AI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FixIt.Tests.Controllers;

public class AnalysisControllerTests
{
    [Fact]
    public async Task TranslateIssueSearch_WithMissingQuery_ReturnsBadRequest()
    {
        var analysisServiceMock = new Mock<FixIt.Services.AI.IIssueAnalysisService>();
        var civicAiServiceMock = new Mock<ICivicAiService>();
        var loggerMock = new Mock<ILogger<AnalysisController>>();
        var controller = new AnalysisController(analysisServiceMock.Object, civicAiServiceMock.Object, loggerMock.Object);

        var result = await controller.TranslateIssueSearch(new IssueSearchTranslationRequest { Query = " " }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task TranslateIssueSearch_WithValidQuery_ReturnsOk()
    {
        var analysisServiceMock = new Mock<FixIt.Services.AI.IIssueAnalysisService>();
        var civicAiServiceMock = new Mock<ICivicAiService>();
        civicAiServiceMock
            .Setup(service => service.TranslateIssueFilterAsync(It.IsAny<IssueFilterTranslationInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IssueFilterTranslationResult
            {
                SearchQuery = "pothole",
                Priority = 2,
                Category = "Infrastructure",
                AiGenerated = true
            });

        var loggerMock = new Mock<ILogger<AnalysisController>>();
        var controller = new AnalysisController(analysisServiceMock.Object, civicAiServiceMock.Object, loggerMock.Object);

        var result = await controller.TranslateIssueSearch(new IssueSearchTranslationRequest { Query = "high priority potholes" }, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task SuggestIssueDraft_WithMissingDraftText_ReturnsBadRequest()
    {
        var analysisServiceMock = new Mock<FixIt.Services.AI.IIssueAnalysisService>();
        var civicAiServiceMock = new Mock<ICivicAiService>();
        var loggerMock = new Mock<ILogger<AnalysisController>>();
        var controller = new AnalysisController(analysisServiceMock.Object, civicAiServiceMock.Object, loggerMock.Object);

        var result = await controller.SuggestIssueDraft(new IssueDraftSuggestionRequest(), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }
}
