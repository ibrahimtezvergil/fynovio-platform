using Contracts;
using CRM.Domain;
using Xunit;

namespace CRM.Tests.Domain;

public sealed class PipelineStageKindTests
{
    [Fact]
    public void Create_defaults_to_open_kind()
    {
        var stage = PipelineStage.Create(TestData.NextTenant(), 1, "Qualification", 10, true);

        Assert.Equal(PipelineStageKind.Open, stage.Kind);
    }

    [Fact]
    public void Create_accepts_an_explicit_kind()
    {
        var stage = PipelineStage.Create(TestData.NextTenant(), 1, "Won", 990, false, PipelineStageKind.Won);

        Assert.Equal(PipelineStageKind.Won, stage.Kind);
    }
}
