using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using PRN222.BLL.Services;
using PRN222.BLL.Services.Interfaces;
using PRN222.Web.Pages.CodeRunner;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace PRN222.Tests.Services;

public class CodeExecutionIntegrationTests
{
    private readonly RoslynCodeExecutionService _service;

    public CodeExecutionIntegrationTests()
    {
        _service = new RoslynCodeExecutionService();
    }

    [Fact]
    public async Task ExecuteCSharpAsync_NormalExecution_ReturnsCorrectOutput()
    {
        // Arrange
        var code = "Console.WriteLine(\"Hello\");";

        // Act
        var result = await _service.ExecuteCSharpAsync(code);

        // Assert
        result.Trim().Should().EndWith("Hello");
    }

    [Fact]
    public async Task ExecuteCSharpAsync_InfiniteLoop_TerminatedByTimeout()
    {
        // Arrange
        var code = "while(true) {}";

        // Act
        var result = await _service.ExecuteCSharpAsync(code);

        // Assert
        result.Should().Contain("timed out");
    }

    [Fact]
    public async Task ExecuteCSharpAsync_CompilationError_ReturnsHelpfulErrorMessage()
    {
        // Arrange
        var code = "invalid code";

        // Act
        var result = await _service.ExecuteCSharpAsync(code);

        // Assert
        result.Should().StartWith("Error:");
    }

    [Fact]
    public async Task ExecuteCSharpAsync_LinqQuery_WorksCorrectly()
    {
        // Arrange
        var code = "var list = new List<int>{1,2}; Console.Write(list.Sum());";

        // Act
        var result = await _service.ExecuteCSharpAsync(code);

        // Assert
        result.Should().Be("3");
    }

    [Fact]
    public async Task CodeRunnerPage_Execute_ReturnsJsonResult()
    {
        // Arrange
        var mockService = new Mock<ICodeExecutionService>();
        mockService.Setup(s => s.ExecuteCSharpAsync(It.IsAny<string>()))
            .ReturnsAsync("Mocked Output");
        
        var page = new IndexModel(mockService.Object, EnabledConfiguration());
        var request = new CodeRequest { Code = "Console.Write(\"test\");" };

        // Act
        var result = await page.OnPostExecuteAsync(request);

        // Assert
        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var value = jsonResult.Value;
        
        // Use reflection to check the property value of the anonymous object
        var outputProperty = value?.GetType().GetProperty("output");
        outputProperty?.GetValue(value).Should().Be("Mocked Output");
    }

    [Fact]
    public async Task CodeRunnerPage_Execute_EmptyCode_ReturnsBadRequest()
    {
        // Arrange
        var mockService = new Mock<ICodeExecutionService>();
        var page = new IndexModel(mockService.Object, EnabledConfiguration());
        var request = new CodeRequest { Code = "" };

        // Act
        var result = await page.OnPostExecuteAsync(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CodeRunnerPage_Execute_WhenFeatureDisabled_ReturnsNotFoundWithoutExecuting()
    {
        var mockService = new Mock<ICodeExecutionService>();
        var configuration = new ConfigurationBuilder().Build();
        var page = new IndexModel(mockService.Object, configuration);

        var result = await page.OnPostExecuteAsync(
            new CodeRequest { Code = "Console.WriteLine(\"test\");" });

        result.Should().BeOfType<NotFoundResult>();
        mockService.Verify(
            s => s.ExecuteCSharpAsync(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task CodeRunnerPage_Execute_WhenCodeExceedsLimit_ReturnsBadRequest()
    {
        var mockService = new Mock<ICodeExecutionService>();
        var page = new IndexModel(mockService.Object, EnabledConfiguration());

        var result = await page.OnPostExecuteAsync(
            new CodeRequest { Code = new string('x', 10_001) });

        result.Should().BeOfType<BadRequestObjectResult>();
        mockService.Verify(
            s => s.ExecuteCSharpAsync(It.IsAny<string>()),
            Times.Never);
    }

    private static IConfiguration EnabledConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Features:CodeExecution:Enabled"] = "true"
            })
            .Build();
    }
}
