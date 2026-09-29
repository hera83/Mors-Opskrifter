using System.Text.Json.Serialization;

namespace web.Controllers.Api.Dto
{
    /// <summary>Sortering af opskriftslisten.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<RecipeSort>))]
    public enum RecipeSort
    {
        /// <summary>Alfabetisk efter titel.</summary>
        Title,

        /// <summary>Senest ændrede først.</summary>
        LastModified,

        /// <summary>Hurtigste (forberedelse + tilberedning) først.</summary>
        TotalTime,
    }
}
