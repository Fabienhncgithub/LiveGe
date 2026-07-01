namespace FrontiereLiveGe.Api.Models;

public class BorderPoint
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public bool IsActive { get; set; } = true;

    public List<TrafficSnapshot> TrafficSnapshots { get; set; } = new();
    public List<AlertEvent> AlertEvents { get; set; } = new();
}
