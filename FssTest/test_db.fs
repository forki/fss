﻿module test_db
open NUnit.Framework

open Fss.Data.Postgres
open System.IO

let createT1SQL = """
create table Test1 (
	id integer NOT NULL,
	age   integer NOT NULL,
	first	varchar(100) NOT NULL,
	last   varchar(100) NOT NULL,
	rate   float NOT NULL
)"""

type Test1 = { id : int ; age : int ; first : string ; last : string ; rate : float}

let t1a = { id = 1 ; age = 30 ; first = "fred" ; last = "flintstone" ; rate = 1.2}

let createT2SQL = """
create table Test2 (
	id serial,
	age   integer  NOT NULL,
	first	varchar(100) NOT NULL,
	last   varchar(100) NOT NULL,
	rate   float NOT NULL default 999.0
)"""


let createT3SQL = """
create table Test3 (
	id serial,
	age   integer NOT NULL,
	first	varchar(100) NOT NULL,
	last   varchar(100),
	rate   float NOT NULL
)"""

type Test3 = { id : int ; age : int ; first : string ; last : string option ; rate : float}

let t3a = { id = 1 ; age = 30 ; first = "fred" ; last = Some "flintstone" ; rate = 1.2}
let t3b = { id = -1 ; age = 4 ; first = "dino" ; last = None ; rate = 1.2}

 
let getConnString() =
    if not (File.Exists("connection.txt")) then
        failwithf "ERROR: expected connection.txt file with connstring"
    else 
        System.IO.File.ReadAllText("connection.txt")

// reusable primitives for testing
let gc() = new DynamicSqlConnection(getConnString())
let drop table (conn:DynamicSqlConnection)  = table |> sprintf "drop table if exists %s" 
                                                |> conn.ExecuteScalar |> ignore

let createT1 (conn:DynamicSqlConnection) = conn.ExecuteScalar(createT1SQL) |> ignore
let createT2 (conn:DynamicSqlConnection) = conn.ExecuteScalar(createT2SQL) |> ignore
let createT3 (conn:DynamicSqlConnection) = conn.ExecuteScalar(createT3SQL) |> ignore


let setupT1 (conn:DynamicSqlConnection) = drop "test1" conn ; createT1 conn
let setupT2 (conn:DynamicSqlConnection) = drop "test2" conn ; createT2 conn
let setupT3 (conn:DynamicSqlConnection) = drop "test3" conn ; createT3 conn


[<TestFixture>]
type TestPGDbBasic() = class     
    

    [<Test>]
    member x.Test001ConnectionDotTxtPresent() =
        Assert.IsTrue(File.Exists("connection.txt"))        

    [<Test>]
    member x.Test002GetConnString() =
        getConnString() |> ignore

    [<Test>]
    member x.Test003Connection() =
        use conn = gc()
        ()

    [<Test>]
    member x.Test004CondDropTable() =
        use conn = gc()
        drop "test1" conn

    [<Test>]
    member x.Test005DropCreate() =
        use conn = gc()
        drop "test1" conn
        createT1 conn
        drop "test1" conn

    [<Test>]
    member x.Test006SingleSimpleInsert() =
        use conn = gc()
        setupT1 conn
        conn.InsertOne(t1a) 
        ()

    [<Test>]
    member x.Test007SingleSimpleInsertWithCheck() =
        use conn = gc()
        setupT1 conn
        conn.InsertOne(t1a)
        Assert.IsTrue(conn.ExecuteScalar "select count(*) from test1" :?> int64 = 1L) |> ignore
        ()

    /// Test serial and default fields are provided by db
    [<Test>]
    member x.Test010SingleSerialInsertWithCheck() =
        use conn = gc()
        setupT2 conn
        conn.InsertOne(t1a,"test2",ignoredColumns=["id" ; "rate"])
        Assert.GreaterOrEqual(conn.ExecuteScalar "select rate from test2 where first = 'fred'" :?> float,998.9)

    /// Insert a record that contains nullable fields which are defined
    [<Test>]
    member x.Test030InsertNullableSimple() =
        use conn = gc()
        setupT3 conn
        conn.InsertOne(t3a,ignoredColumns=["id"]) |> ignore

    /// Insert Test3 example with nulled field last
    [<Test>]
    member x.Test031InsertNullableNull() =
        use conn = gc()
        setupT3 conn
        conn.InsertOne(t3b) |> ignore

    [<Test>]
    member x.Test032InsertManySomeNull() =
        use conn = gc()
        setupT3 conn
        conn.InsertMany([ t3a; t3b]) |>  ignore

    [<Test>]
    member x.Test033QuerySomeNull() =
        use conn = gc()
        setupT3 conn
        conn.InsertMany([ t3a; t3b]) |> ignore
        let results = conn.Query "select * from test3"  |> Array.ofSeq

        let (someLast,noneLast) = results |> Array.partition (fun r -> r.last.IsSome)
        Assert.Equals(someLast.Length,1) |> ignore
        Assert.Equals(noneLast.Length,1) |> ignore

end
