namespace RasStudio.Application.RasGates;

public interface IRasGateService
{
    Task<RasGatePage> GetPageAsync(
        int page,
        int pageSize,
        string? query = null,
        IReadOnlyCollection<RasGateSearchField>? fields = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RasGate>> GetAllAsync(
        string? query = null,
        IReadOnlyCollection<RasGateSearchField>? fields = null,
        CancellationToken cancellationToken = default);

    Task<RasGate> GetAsync(
        Guid rasGateId,
        CancellationToken cancellationToken = default);

    Task<RasGate> CreateAsync(
        CreateRasGate command,
        CancellationToken cancellationToken = default);

    Task<RasGate> UpdateAsync(
        Guid rasGateId,
        UpdateRasGate command,
        CancellationToken cancellationToken = default);

    Task<RasGate> DeleteAsync(
        Guid rasGateId,
        CancellationToken cancellationToken = default);

    Task<RasGateStatus> GetShadowStatusAsync(
        Guid rasGateId,
        CancellationToken cancellationToken = default);

    Task<RasGateStatus> RefreshStatusAsync(
        Guid rasGateId,
        CancellationToken cancellationToken = default);
}
