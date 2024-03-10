module Redasher.Tests

open Expecto
open Redash

let [<Literal>] dataSourcesResponse = """
[
  {
    "id": 436,
    "name": "db1",
    "type": "rds_mysql",
    "syntax": "sql",
    "paused": 0,
    "pause_reason": null,
    "supports_auto_limit": true,
    "view_only": false
  },
  {
    "id": 437,
    "name": "db2",
    "type": "rds_mysql",
    "syntax": "sql",
    "paused": 0,
    "pause_reason": null,
    "supports_auto_limit": true,
    "view_only": false
  },
  {
    "id": 367,
    "name": "db3",
    "type": "clickhouse",
    "syntax": "sql",
    "paused": 0,
    "pause_reason": null,
    "supports_auto_limit": true,
    "view_only": false
  }]
"""

let [<Literal>] jobResponse = """
{
  "job": {
    "id": "ffe907d3-d548-4e6e-a264-af7d1bbe21ed",
    "updated_at": 0,
    "status": 3,
    "error": "",
    "result": 282730,
    "query_result_id": 282730
  }
}
"""

let [<Literal>] jobWithNullResponse = """
{"job": {"id": "7d091f30-a8ed-4bf9-92d7-bdc29333fe44", "updated_at": 0, "status": 1, "error": "", "result": null, "query_result_id": null}}
"""

let [<Literal>] queryResultResponse = """
{
  "query_result": {
    "id": 282730,
    "query_hash": "02b7fb91622ec1850dacfbd3c10d6839",
    "query": "SELECT * FROM user LIMIT 2",
    "data": {
      "columns": [
        {
          "name": "id",
          "friendly_name": "id",
          "type": "integer"
        },
        {
          "name": "base_params",
          "friendly_name": "base_params",
          "type": null
        },
        {
          "name": "transport",
          "friendly_name": "transport",
          "type": null
        },
        {
          "name": "phone_number",
          "friendly_name": "phone_number",
          "type": "string"
        }
      ],
      "rows": [
        {
          "id": 1,
          "base_params": "{\"name\": \"Иван\", \"avatar\": \"ava.jpeg\", \"city_id\": 1, \"country_id\": 1}",
          "transport": "{\"kind\": \"car\", \"brand\": \"Audi\", \"color\": \"lightblue\", \"model\": \"Q8\", \"comfort_level\": \"economy\"}",
          "phone_number": "35700000000"
        },
        {
          "id": 2,
          "base_params": "{\"name\": \"Иннокентий\", \"avatar\": \"ava2.jpeg\", \"city_id\": 1, \"country_id\": 1}",
          "transport": "{}",
          "phone_number": "7770000000"
        }
      ]
    },
    "data_source_id": 43,
    "runtime": 0.9931063652038574,
    "retrieved_at": "2024-03-08T18:29:22.246Z"
  }
}
"""

let mockConnectionInfo = {ConnectionUrl = "fake"; ApiKey = "fake" }

let datasourceLoadTests = testList "Load DataSources" [
   test "LoadDataSourcesOK" {
      let mockRequester _ _ =
        System.Text.Encoding.UTF8.GetBytes dataSourcesResponse
      let result = getDataSources' mockRequester mockConnectionInfo ""
      let expected = 3
      Expect.equal result.Length expected
        $"We got {result.Length}, but expected {expected}"
   }
   
   test "Get data source by name" {
     let mockRequester _ _ =
             System.Text.Encoding.UTF8.GetBytes dataSourcesResponse
     let dsName = "db2"
     let result = getDataSource' mockRequester mockConnectionInfo "endpoint" dsName
     Expect.isSome result
       $"expected data source with name {dsName}, but got Nothing"
     Expect.equal result.Value.Name dsName
       $"expected data source with name {dsName}, but got {result.Value.Name}"
   }
   
   test "Source is absent" {
     let mockRequester _ _ =
                 System.Text.Encoding.UTF8.GetBytes "[]"
     let dsName = "db2"
     let result = getDataSource' mockRequester mockConnectionInfo "endpoint" dsName
     Expect.isNone result $"Expected empty value, but got {result}"
   }
]

let jobLoadTests = testList "Load Job by ID" [
  test "Load JOB is OK" {
    let mockRequester _ _ =
            System.Text.Encoding.UTF8.GetBytes jobResponse
    let result = getJob' mockRequester mockConnectionInfo ""
    let expected = "ffe907d3-d548-4e6e-a264-af7d1bbe21ed"
    Expect.equal result.Id expected
      $"Wrong job's id, expected: {expected}, but got {result.Id}"
  }
  
  test "Load JOB some fields have NULL values" {
     let mockRequester _ _ =
                System.Text.Encoding.UTF8.GetBytes jobWithNullResponse
     let result = getJob' mockRequester mockConnectionInfo ""
     let expected = "7d091f30-a8ed-4bf9-92d7-bdc29333fe44"
     Expect.equal result.Id expected
          $"Wrong job's id, expected: {expected}, but got {result.Id}"
  }
]

let queryResultLoadTests = testList "Load QueryResult by ID" [
  test "Load QueryResult is OK" {
    let mockRequester _ _ =
            System.Text.Encoding.UTF8.GetBytes queryResultResponse
    let result = getQueryResult'<{|id: int|}> mockRequester mockConnectionInfo ""
    let expectedLen = 2
    let rowsLen = result.Data.Rows.Length
    Expect.equal rowsLen expectedLen
       $"Wrong job's id, expected: {expectedLen}, but got {rowsLen}"
  }
  
  test "Query Results (twice invocation)" {
    let mockFirstRequester _ _ _ =
            System.Text.Encoding.UTF8.GetBytes jobResponse
            
    let mockSecondRequester _ _ _ =
            System.Text.Encoding.UTF8.GetBytes queryResultResponse
            
    let queryResultHelper requester =
      let dataSource = {Id = 1; Name = "db1"; Type = "rds"; Syntax = "mysql" }
      getQueryResults'<{|id: int|}> requester
                            mockConnectionInfo
                            ""
                            dataSource
                            "SELECT * FROM users LIMIT 1"
            
    let queryResults1 =
      queryResultHelper mockFirstRequester
    Expect.isSome queryResults1.Job $"Expected Job first"
    
    let queryResults2 =
      queryResultHelper mockSecondRequester
    Expect.isSome queryResults2.QueryResult $"Expected QueryResult second" 
  }
]
