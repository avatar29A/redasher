module Redash

open System
open System.Text.Json
open System.Text.Json.Serialization
open FsHttp

type ConnectionInfo = {
    ConnectionUrl : string
    ApiKey : string
} with
    member this.MakeFullUrl(part:string) =
        let baseUrl = Uri(this.ConnectionUrl)
        let endpoint = Uri(baseUrl, part)
        endpoint.ToString ()
        
type Requester = ConnectionInfo -> string -> byte array
type PostRequester = ConnectionInfo -> string -> string -> byte array

type JobStatus = | Pending = 1
                 | Started = 2
                 | Success = 3
                 | Failure = 4
                 | Canceled = 5

type Job = {
    [<JsonName "id">]               Id: string
    [<JsonName "updated_at">]       UpdatedAt: obj
    [<JsonName "status">]           Status: JobStatus
    [<JsonName "error">]            Error: string
    [<JsonName "result">]           Result: int
    [<JsonName "query_result_id">]  QueryResultId: int
}

type QueryResult<'Model> = {
    [<JsonName "id">]               Id: int
    [<JsonName "query_hash">]       Hash: string
    [<JsonName "query">]            Query: string
    [<JsonName "data">]             Data: QueryResultData<'Model>
    [<JsonName "data_source_id">]   DataSourceId: int
    [<JsonName "runtime">]          Runtime: float
    [<JsonName "retrieved_at">]     RetrievedAt: string
}
and QueryResultData<'Model> = {
    [<JsonName "columns">]          Columns: QueryResultColumn list
    [<JsonName "rows">]             Rows: 'Model list
}
and QueryResultColumn = {
     [<JsonName "name">]            Name: string
     [<JsonName "friendly_name">]   FriendlyName: string
     [<JsonName "type">]            Type: string
}

type QueryResults<'Model> = {
    [<JsonName "query_result">]
    QueryResult: QueryResult<'Model> option
    [<JsonName "job">]
    Job: Job option
}

type Datasource = {
    [<JsonName "id">]       Id: int
    [<JsonName "name">]     Name: string
    [<JsonName "type">]     Type: string
    [<JsonName "syntax">]   Syntax: string
}

let private getJsonSerializerOptions =
    JsonFSharpOptions.Default()
        .WithAllowNullFields()
        .WithSkippableOptionFields()
        .WithUnwrapOption()
        .ToJsonSerializerOptions()
        
        
///
/// Utils
///

// bs2str convert bytes which have been retrived from API to UTF8 string
let private bs2str (bs : byte array) : string =
    System.Text.Encoding.UTF8.GetString bs
    
let private printResponse (response:string) =
    printfn $"{response}"
    response

// toJson helper to convert string in Object
let private toJson<'T> (json:string) =
     let options = getJsonSerializerOptions
     JsonSerializer.Deserialize<'T>(json, options)
 
///
/// Predefined HTTP Requesters

let private get (connection:ConnectionInfo) (endpoint:string) =
     http {
            GET endpoint
            Authorization $"Key {connection.ApiKey}"
     }
     |> Request.send
     |> Response.assertOk
     |> Response.toBytes

let private post (connection:ConnectionInfo)
            (endpoint:string)
            (jsonPayload:string)=
    http {
        POST endpoint
        Authorization $"Key {connection.ApiKey}"
        body
        json jsonPayload
    }
    |> Request.send
    |> Response.assertOk
    |> Response.toBytes

//
// getDataSource retrieves information about all available data sources
// It's important for next queries where we should pass data source's id.
    
let getDataSources' (requester:Requester)
                           (connection:ConnectionInfo)
                           (endpoint:string) =
    requester connection endpoint
    |> bs2str
    |> toJson<Datasource list>

let getDataSources (connection:ConnectionInfo) : Datasource list =
    let endpoint = connection.MakeFullUrl "api/data_sources"
    getDataSources' get connection endpoint
    
let getDataSource' (requester:Requester)
                   (connection:ConnectionInfo)
                   (endpoint:string)
                   (name:string) : Datasource option =
    try
        let dataSources = getDataSources' requester connection endpoint
        dataSources
        |> List.find (fun ds -> ds.Name = name)
        |> Some
    with _ ->
        None
        
let getDataSource (connection:ConnectionInfo) (name:string) : Datasource option =
    let endpoint = connection.MakeFullUrl "api/data_sources"
    getDataSource' get connection endpoint name
    
//
// getJob retrieves stats of working job

let getJob' (requester:Requester)
            (connection:ConnectionInfo)
            (endpoint:string) =
    requester connection endpoint
    |> bs2str
    |> toJson<{|job: Job|}>
    |> _.job

let getJob (connection:ConnectionInfo) (id:string) : Job =
    let endpoint = connection.MakeFullUrl $"api/jobs/{id}"
    getJob' get connection endpoint
    
//
// getQueryResults
let getQueryResult'<'Model> (requester:Requester)
                    (connection:ConnectionInfo)
                    (endpoint:string) =
    requester connection endpoint
    |> bs2str
    |> toJson<{|query_result:(QueryResult<'Model>)|}>
    |> _.query_result

let getQueryResult<'Model> (connection:ConnectionInfo) (id: int) =
    let endpoint = connection.MakeFullUrl $"api/query_results/{id}"
    getQueryResult'<'Model> get connection endpoint
    
let getQueryResults'<'Model>  (requester:PostRequester)
                                  (connection:ConnectionInfo)
                                  (endpoint:string)
                                  (ds: Datasource)
                                  (q: string) =
    let payload = $"""
    {{
        "data_source_id": {ds.Id},
        "query": "{q}"
    }}
    """
    requester connection endpoint payload
    |> bs2str
    |> toJson<QueryResults<'Model>>
    
let rec getQueryResults<'Model> (connection:ConnectionInfo)
                                (ds: Datasource)
                                (q:string) =
    let endpoint = connection.MakeFullUrl $"api/query_results"
    let queryResults = getQueryResults'<'Model> post connection endpoint ds q
    match queryResults.QueryResult, queryResults.Job with
    | None, Some job
        when job.Status = JobStatus.Success ->
        getQueryResults connection ds q
    | None, Some job
        when job.Status = JobStatus.Canceled ||
             job.Status = JobStatus.Failure ->
        raise (TimeoutException($"Time is out or job was failed (status: {job.Status})"))
    | None, Some _ ->
        Async.Sleep (300)
        |> Async.RunSynchronously
        getQueryResults connection ds q
    | Some qr, _ -> qr
    | None, None -> raise (JsonException("No valid json object in response"))
