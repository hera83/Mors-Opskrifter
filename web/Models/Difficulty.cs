using System.Text.Json.Serialization;

namespace web.Models
{
    [JsonConverter(typeof(JsonStringEnumConverter<Difficulty>))]
    public enum Difficulty { Let, Middel, Svær }
}
