namespace Model.Core.Player;

/// <summary>
/// Something one civilisation has put to another and is waiting on an answer to.
/// <para>
/// The kind of proposal is a plain string rather than an enum belonging to the
/// engine, so that the interface can describe it without the model having to know
/// what the engine's diplomacy offers. <see cref="Gold"/> and
/// <see cref="Advance"/> carry what is being offered where the proposal is a
/// gift.
/// </para>
/// </summary>
public class DiplomaticProposal
{
    public required string Kind { get; init; }

    public int Gold { get; init; }

    public int Advance { get; init; } = -1;
}
