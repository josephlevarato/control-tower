namespace Shared;

using System.Text.Json;

public static class PosJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}