using FluentAssertions;
using KJ.Workflows;
using Xunit;

namespace KJ.Workflows.Tests;

public class ExternalStepHandlerTests
{
    private static WorkflowExecutionContext FakeCtx()
    {
        var logs = new List<WorkflowRunLogEntry>();
        return new WorkflowExecutionContext(Guid.NewGuid(), e => logs.Add(e));
    }

    // ── HttpStepHandler ──────────────────────────────────────────────────

    [Fact]
    public void HttpStepHandler_CanHandle_Http()
    {
        var handler = new HttpStepHandler(new HttpClient());
        handler.CanHandle("Http").Should().BeTrue();
        handler.CanHandle("HttpRequest").Should().BeTrue();
        handler.CanHandle("Webhook").Should().BeTrue();
        handler.CanHandle("Shell").Should().BeFalse();
    }

    [Fact]
    public async Task HttpStepHandler_ShouldThrow_WhenNoUrl()
    {
        var handler = new HttpStepHandler(new HttpClient());
        var step = new WorkflowStep { Title = "Test", Kind = "Http", Parameters = new() };
        var ctx = FakeCtx();

        var act = () => handler.ExecuteAsync(step, ctx, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*url*");
    }

    // ── ShellStepHandler ─────────────────────────────────────────────────

    [Fact]
    public void ShellStepHandler_CanHandle()
    {
        var handler = new ShellStepHandler();
        handler.CanHandle("Shell").Should().BeTrue();
        handler.CanHandle("Command").Should().BeTrue();
        handler.CanHandle("Exec").Should().BeTrue();
        handler.CanHandle("Http").Should().BeFalse();
    }

    [Fact]
    public async Task ShellStepHandler_ShouldThrow_WhenNoCommand()
    {
        var handler = new ShellStepHandler();
        var step = new WorkflowStep { Title = "Test", Kind = "Shell", Parameters = new() };
        var ctx = FakeCtx();

        var act = () => handler.ExecuteAsync(step, ctx, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*command*");
    }

    [Fact]
    public async Task ShellStepHandler_ShouldExecuteCommand()
    {
        var handler = new ShellStepHandler();
        var step = new WorkflowStep
        {
            Title = "Echo",
            Kind = "Shell",
            Parameters = new()
            {
                ["command"] = "echo",
                ["args"] = "hello world",
                ["resultVar"] = "output",
            }
        };
        var ctx = FakeCtx();

        await handler.ExecuteAsync(step, ctx, CancellationToken.None);

        step.Parameters.Should().ContainKey("__result:output");
        step.Parameters["__result:output"].Should().Contain("hello world");
    }

    [Fact]
    public async Task ShellStepHandler_ShouldStoreResult()
    {
        var handler = new ShellStepHandler();
        var step = new WorkflowStep
        {
            Title = "Echo",
            Kind = "Shell",
            Parameters = new()
            {
                ["command"] = "echo",
                ["args"] = "test output",
                ["resultVar"] = "output",
            }
        };
        var ctx = FakeCtx();

        await handler.ExecuteAsync(step, ctx, CancellationToken.None);

        step.Parameters["__result:output"].Should().Contain("test output");
    }

    [Fact]
    public async Task ShellStepHandler_ShouldFail_WhenCommandFails()
    {
        var handler = new ShellStepHandler();
        var step = new WorkflowStep
        {
            Title = "Fail",
            Kind = "Shell",
            Parameters = new()
            {
                ["command"] = "false",
            }
        };
        var ctx = FakeCtx();

        var act = () => handler.ExecuteAsync(step, ctx, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*exit code*");
    }

    // ── MqttStepHandler ──────────────────────────────────────────────────

    [Fact]
    public void MqttStepHandler_CanHandle()
    {
        var handler = new MqttStepHandler();
        handler.CanHandle("Mqtt").Should().BeTrue();
        handler.CanHandle("MqttPublish").Should().BeTrue();
        handler.CanHandle("Http").Should().BeFalse();
    }

    [Fact]
    public async Task MqttStepHandler_ShouldThrow_WhenNoBrokerOrTopic()
    {
        var handler = new MqttStepHandler();
        var step = new WorkflowStep { Title = "Test", Kind = "Mqtt", Parameters = new() };
        var ctx = FakeCtx();

        var act = () => handler.ExecuteAsync(step, ctx, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*broker*");
    }

    // ── DatabaseStepHandler ──────────────────────────────────────────────

    [Fact]
    public void DatabaseStepHandler_CanHandle()
    {
        var handler = new DatabaseStepHandler();
        handler.CanHandle("Database").Should().BeTrue();
        handler.CanHandle("Sql").Should().BeTrue();
        handler.CanHandle("DbQuery").Should().BeTrue();
        handler.CanHandle("Http").Should().BeFalse();
    }

    [Fact]
    public async Task DatabaseStepHandler_ShouldThrow_WhenNoQuery()
    {
        var handler = new DatabaseStepHandler();
        var step = new WorkflowStep { Title = "Test", Kind = "Database", Parameters = new() };
        var ctx = FakeCtx();

        var act = () => handler.ExecuteAsync(step, ctx, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*query*");
    }
}
