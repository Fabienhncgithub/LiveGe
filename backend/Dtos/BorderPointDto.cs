namespace FrontiereLiveGe.Api.Dtos;

public class BorderPointDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public bool IsActive { get; set; }
}
