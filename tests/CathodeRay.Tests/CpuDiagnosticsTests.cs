using CathodeRay.Abstractions;
using CathodeRay.Stub;
using FluentAssertions;

namespace CathodeRay.Tests;

public sealed class CpuDiagnosticsTests
{
    private sealed class RecordingObserver : ICpuExecutionObserver
    {
        public List<CpuStepTrace> Traces { get; } = [];

        public List<Exception> Failures { get; } = [];

        public Func<CpuDebugSnapshot, bool>? BreakWhen { get; set; }

        public bool ShouldBreak(CpuDebugSnapshot snapshot) => BreakWhen?.Invoke(snapshot) == true;

        public void OnStepCompleted(CpuStepTrace trace) => Traces.Add(trace);

        public void OnStepFailed(CpuDebugSnapshot snapshot, Exception exception) => Failures.Add(exception);
    }

    private sealed class PlainCpu : ICpu<StubState>
    {
        private readonly int _cycles;

        public PlainCpu(int cycles) => _cycles = cycles;

        public StubState State { get; } = new();

        public int Step()
        {
            State.ProgramCounter++;
            return _cycles;
        }

        public void Reset() => State.ProgramCounter = 0;
    }

    private static StubIsa Isa()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "data", "instructions", "mcp_stub_instructions.json")))
        {
            dir = dir.Parent;
        }

        return StubIsa.FromJsonFile(Path.Combine(dir!.FullName, "data", "instructions", "mcp_stub_instructions.json"));
    }

    private static StubCpu Cpu(params byte[] program)
    {
        var bus = new StubBus();
        for (int i = 0; i < program.Length; i++)
        {
            bus.Write((ushort)i, program[i]);
        }

        return new StubCpu(Isa(), bus);
    }

    [Fact]
    public void Step_Emits_Trace_And_Status()
    {
        var observer = new RecordingObserver();
        var diagnostics = new CpuDiagnostics<StubState>(Cpu(0x01, 0x2A), observer);

        diagnostics.HasStatus.Should().BeTrue();
        CpuStepTrace trace = diagnostics.Step();

        observer.Traces.Should().ContainSingle();
        trace.Cycles.Should().Be(2);
        trace.Before.LastOpcode.Should().Be(-1);
        trace.After.LastOpcode.Should().Be(0x01);
        trace.After.LastMnemonic.Should().Be("LDI");
        trace.After.ProgramCounter.Should().Be(2);
        trace.After.CycleCount.Should().Be(2);
        trace.After.InstructionCount.Should().Be(1);
    }

    [Fact]
    public void BusActivity_Is_Recorded()
    {
        var observer = new RecordingObserver();
        var diagnostics = new CpuDiagnostics<StubState>(Cpu(0x01, 0x10, 0x05, 0x20, 0x00, 0xFF), observer);

        diagnostics.Step();
        diagnostics.Step();
        diagnostics.Step();

        observer.Traces[0].After.LastBusActivity.Should().HaveFlag(BusActivity.Read);
        observer.Traces[1].After.LastBusActivity.Should().HaveFlag(BusActivity.Write);
        observer.Traces[2].After.LastBusActivity.Should().HaveFlag(BusActivity.Halt);
    }

    [Fact]
    public void Run_Stops_At_Breakpoint()
    {
        var observer = new RecordingObserver { BreakWhen = snapshot => snapshot.ProgramCounter >= 3 };
        var diagnostics = new CpuDiagnostics<StubState>(Cpu(0x00, 0x00, 0x00, 0x00, 0x00), observer);

        diagnostics.Run(10).Should().HaveCount(3);
    }

    [Fact]
    public void StepFailed_Is_Reported_And_Rethrown()
    {
        var observer = new RecordingObserver();
        var diagnostics = new CpuDiagnostics<StubState>(Cpu(0x7F), observer);

        FluentActions.Invoking(diagnostics.Step).Should().Throw<InvalidOperationException>();
        observer.Failures.Should().ContainSingle();
        observer.Traces.Should().BeEmpty();
    }

    [Fact]
    public void Degrades_Without_Status()
    {
        var observer = new RecordingObserver();
        var diagnostics = new CpuDiagnostics<StubState>(new PlainCpu(3), observer);

        diagnostics.HasStatus.Should().BeFalse();
        CpuStepTrace trace = diagnostics.Step();

        trace.Cycles.Should().Be(3);
        trace.After.Should().Be(default(CpuDebugSnapshot));
        observer.Traces.Should().ContainSingle();
    }
}
