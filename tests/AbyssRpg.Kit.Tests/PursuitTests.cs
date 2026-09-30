using AbyssRpg.Kit.Ai;
using Xunit;

namespace AbyssRpg.Kit.Tests;

public sealed class PursuitTests
{
    [Fact]
    public void Pursuit_memory_keeps_the_previous_state_for_a_ruleset_transition_fact()
    {
        PursuitMemoryComponent memory = new();

        Assert.Equal(PursuitState.Idle, memory.TransitionTo(PursuitState.Chase));
        Assert.Equal(PursuitState.Chase, memory.TransitionTo(PursuitState.Attack));
        Assert.Equal(PursuitState.Attack, memory.State);
    }
}
