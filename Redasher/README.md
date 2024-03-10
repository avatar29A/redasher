# Redasher

F# library for Redash API

## Install

Install from NuGet (_in progress_)

```sh
> dotnet add package Redasher
```

## How Usage

Before using library you should have 

- Url of your redash server
- Api Key for you user

1. Open Redasher library and prepare you ConnectionInfo object

```fsharp
open System
open Redash

let connectionInfo = {
    ApiKey = Environment.GetEnvironmentVariable "ApiKey"
    ConnectionUrl = Environment.GetEnvironmentVariable "BaseUrl"
}
```

_Note_: In example we have taken sensitivity data from Environment variables.

2. Load information about datasource by his name

```fsharp
let ds = getDataSource connectionInfo "my-data-source"
```

3. Invoke query
```fsharp
let queryResult = getQueryResults<{|id: int; name: string|}> connectionInfo
                      ds.Value
                      "SELECT id FROM user LIMIT 5"
                   
for row in queryResult.Data.Rows do
    printfn $"User {row.id} {row.name}"
```

## Support API

Official documentation: https://redash.io/help/user-guide/integrations-and-api/api

### DataSources

**GET** `/api/data_sources`

#### List of accessible datasources

```fsharp
let dataSources = getDataSources connectionInfo
```
> dataSources: Datasource list

#### Retrieve datasource bi ID

```fsharp
let ds = getDataSource connectionInfo "data-source-name"
```
> ds: Datasource

### Queries

**POST** `/api/query_results`

Primary method for extracting data from datasource. This method hide internal flow of Redash and implicitly from you 
 - create query
 - polling job
 - retrieve information after job will be done

Firstly we should specify model for query.

**Example**:

Suppose we have a table users with three columns: 
 
    - id: int
    - name: string
    - age: int

Declare model for that in F#

```fsharp
let User = type {
    id: int
    name: string
    age: int
}
```

and make query for this table like: 'SELECT * FROM users LIMIT 5'

```fsharp
let queryResult = getQueryResults<User> connectionInfo 
                                        "data-source-name"
                                        "SELECT * FROM users LIMIT 5"
                                        
for user in queryResult.Data.Rows do
    printfn $"User {user.id}, {user.name}, {user.age}"
```

**GET** `api/query_results/{id}`

Retrieve data from prepared query by id. As we mention above, 
firstly you should prepare model for your query.

```fsharp
let queryResult = getQueryResult<User> connectionInfo 34803 
```

### Jobs

**GET** `/api/jobs/<job_id>`

Retrieves information of working job. Usually jobs are created implicitly after calling 'POST: /api/query_results' 
and you dont need thin about that.

```fsharp
let job = getJob connectionInfo "2cc44526-1d5f-40b6-a380-0aa11247e2c8"
```

Job can be in several statuses:

```fsharp
type JobStatus = | Pending = 1
                 | Started = 2
                 | Success = 3
                 | Failure = 4
                 | Canceled = 5
```
