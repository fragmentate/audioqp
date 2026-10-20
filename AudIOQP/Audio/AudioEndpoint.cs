namespace AudIOQP.Audio;

public enum EndpointStatus
{
    Active = 1,
    Disabled = 2,
    NotPresent = 4,
    Unplugged = 8
}

public sealed record AudioEndpoint(string Id, string FriendlyName, EndpointStatus Status)
{
    public bool IsActive => Status == EndpointStatus.Active;

    public string AvailabilityLabel => Status switch
    {
        EndpointStatus.Active => "",
        EndpointStatus.Disabled => "disabled",
        EndpointStatus.Unplugged => "disconnected",
        _ => "not present"
    };
}

public sealed class DeviceRef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";

    public DeviceRef()
    {
    }

    public DeviceRef(string id, string name)
    {
        Id = id ?? "";
        Name = name ?? "";
    }
}
