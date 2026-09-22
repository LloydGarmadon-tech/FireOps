namespace FireOps.Mvp.Services;

public sealed class ChangeNotifier
{
    public event Action<Guid>? IncidentChanged;

    public void Notify(Guid incidentId) => IncidentChanged?.Invoke(incidentId);
}
