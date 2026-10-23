using System;
using System.Text.Json.Serialization;

namespace NBL.Services;

public class Class1
{
    public Class1()
    {
    }
}

public class ClientsResponse
{
    [JsonPropertyName("clients")]
    public List<ApiClient> Clients { get; set; } = new();
}

public class ApiClient
{
    [JsonPropertyName("ident")]
    public string Ident { get; set; } = "";
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
    [JsonPropertyName("short_name")]
    public string ShortName { get; set; } = "";
    [JsonPropertyName("files")]
    public bool Files { get; set; }
    [JsonPropertyName("reference")]
    public bool Reference { get; set; }
    [JsonPropertyName("address")]
    public string? Address { get; set; }
}

public class ServersResponse
{
    [JsonPropertyName("servers")]
    public List<ApiServer> Servers { get; set; } = new();
}

public class ApiServer
{
    [JsonPropertyName("address")]
    public string Address { get; set; } = "";
    [JsonPropertyName("query_port")]
    public int QueryPort { get; set; }
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("clientid")]
    public string ClientId { get; set; } = "";
}