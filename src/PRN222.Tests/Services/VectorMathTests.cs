using FluentAssertions;
using PRN222.BLL.Services.Rag;

namespace PRN222.Tests.Services;

public class VectorMathTests
{
    [Fact]
    public void CosineSimilarity_ShouldReturnOne_ForIdenticalDirection()
    {
        var result = VectorMath.CosineSimilarity(
            new[] { 1f, 2f, 3f },
            new[] { 1f, 2f, 3f });

        result.Should().BeApproximately(1f, 0.0001f);
    }

    [Fact]
    public void CosineSimilarity_ShouldReturnZero_ForOrthogonalVectors()
    {
        var result = VectorMath.CosineSimilarity(
            new[] { 1f, 0f },
            new[] { 0f, 1f });

        result.Should().Be(0f);
    }

    [Fact]
    public void CosineSimilarity_ShouldReturnZero_ForZeroMagnitudeVector()
    {
        var result = VectorMath.CosineSimilarity(
            new[] { 0f, 0f },
            new[] { 1f, 1f });

        result.Should().Be(0f);
    }

    [Fact]
    public void CosineSimilarity_ShouldUseSharedLength_WhenVectorsHaveDifferentDimensions()
    {
        var result = VectorMath.CosineSimilarity(
            new[] { 1f, 0f, 99f },
            new[] { 1f, 0f });

        result.Should().BeApproximately(1f, 0.0001f);
    }
}
