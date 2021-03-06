﻿module CsvReadTests

open System
open System.IO
open FsUnit
open FsCheck
open NUnit.Framework
open Angara.Data
open Angara.Data.DelimitedFile
open Angara.Data.DelimitedFile.Helpers
open System.Collections
open System.Collections.Immutable

let internal CompareTables (table1: (ColumnSchema * IList) array) (table2: (ColumnSchema * IList) array) = 
    table1.Length |> should equal table2.Length
    for i = 0 to table1.Length - 1 do
        fst table1.[i]|> should equal (fst table2.[i])
        let column1 = table1.[i] |> snd
        let column2 = table2.[i] |> snd
        column1.Count |> should equal column2.Count
        for j = 0 to column1.Count - 1  do
            column1.[j] |> should equal (column2.[j])     

let asReader (content: string) =
    let ms = new MemoryStream()
    let sw = new StreamWriter(ms)
    sw.Write(content)
    sw.Flush()
    ms.Position <- int64(0)
    new StreamReader(ms)

let splitRowStr delimiter (row:string) =
    use r = asReader row
    splitRow delimiter r

let normalizeNL (s:string) =
    if s = null then s else s.Replace("\n", Environment.NewLine)


[<Angara.Data.TestsF.Common.Property; Category("CI")>]
let ``Csv.SplitRow is equivalent to String.Split if no newlines and quotes`` (s:string) =
    let precondition (s:string) = s <> null && not(s.Contains("\"")) && not(s.Contains("\n")) && not(s.Contains("\r"))
    let property s = 
        let splitdata = splitRowStr ',' s
        let items2 = s.Split([| ',' |])
        match splitdata with 
        | Some items-> Angara.Data.TestsF.Common.areEqualStringsForCsv (ImmutableArray.Create<string> items) (ImmutableArray.Create<string> items2)
        | None -> s = ""
    precondition s ==>  lazy(property s)
        
[<Test; Category("CI")>]
let ``Split empty string``() =
     let splitdata = splitRowStr ',' ""
     match splitdata with 
     | None -> ()
     | Some _ -> Assert.Fail()


let TestParseAndEscape stringOfCsv (unescaped:string) (expectedEscaped:string option) = 
    let splitdata = splitRowStr ',' (stringOfCsv + ",a")
    match splitdata with 
    | None -> Assert.Fail()
    | Some items ->         
        items |> should equal [| normalizeNL unescaped; "a" |]
        let escaped = escapeString unescaped "," true
        match expectedEscaped with
        | Some expectedEscaped -> 
            Assert.AreEqual(normalizeNL expectedEscaped, normalizeNL escaped, "Escape")
        | None -> 
            Assert.AreEqual(normalizeNL stringOfCsv, normalizeNL escaped, "Escape")

[<Test; Category("CI")>]
let ``Parse and escape string``() =
     TestParseAndEscape "abc" "abc" None
     TestParseAndEscape "" null None
     TestParseAndEscape "\"\"" "" None
     TestParseAndEscape "\"\"\"\"" "\"" None
     TestParseAndEscape "\"\n\"" "\n" None
     TestParseAndEscape "\"\n\"abc" "\nabc" (Some "\"\nabc\"")
     TestParseAndEscape "\"def\n123\"abc\"" "def\n123abc\"" (Some "\"def\n123abc\"\"\"")
     TestParseAndEscape "\"\"abc" "abc" (Some "abc")
     TestParseAndEscape "\"\"\"\"abc" "\"abc" (Some "\"\"\"abc\"")

[<Angara.Data.TestsF.Common.Property; Category("CI")>]
let ``Escape then parse makes an original string`` (s:string) =
    let escape = escapeString s "," true
    let splitdata = splitRowStr ',' (escape + ",a")
    match splitdata with 
    | None -> false
    | Some items -> 
        items.Length = 2 && items.[0] = s && items.[1] = "a" 


[<Test; Category("CI")>]
let ``Escape then parse makes an original string - case 1`` () =
    let escape = escapeString ">8\n," "," true
    let splitdata = splitRowStr ',' escape
    match splitdata with 
    | None -> Assert.Fail()
    | Some items -> 
        Assert.AreEqual(1,items.Length)
        Assert.AreEqual(normalizeNL ">8\n,", items.[0])

[<Test; Category("CI")>]
let ``Escape then parse makes an original string - case 2`` () =
    let escape = escapeString "\r" "," true
    let splitdata = splitRowStr ',' escape
    match splitdata with 
    | None -> Assert.Fail()
    | Some items -> 
        Assert.AreEqual(1,items.Length)
        Assert.AreEqual(Environment.NewLine, items.[0])

[<Test; Category("CI")>]
let ``Escape then parse makes an original string - case 6`` () =
    let escape = escapeString "\n" "," true
    let splitdata = splitRowStr ',' escape
    match splitdata with 
    | None -> Assert.Fail()
    | Some items -> 
        Assert.AreEqual(1,items.Length)
        Assert.AreEqual(Environment.NewLine, items.[0])

[<Test; Category("CI")>]
let ``Escape then parse makes an original string - case 7`` () =
    let escape = escapeString "\r\n" "," true
    let splitdata = splitRowStr ',' escape
    match splitdata with 
    | None -> Assert.Fail()
    | Some items -> 
        Assert.AreEqual(1,items.Length)
        Assert.AreEqual(Environment.NewLine, items.[0])

[<Test; Category("CI")>]
let ``Escape then parse makes an original string - case 3`` () =
    let escape = escapeString "\n\r" "," true
    let splitdata = splitRowStr ',' escape
    match splitdata with 
    | None -> Assert.Fail()
    | Some items -> 
        Assert.AreEqual(1,items.Length)
        Assert.AreEqual(Environment.NewLine + Environment.NewLine, items.[0])
        

[<Test; Category("CI")>]
let ``Escape then parse makes an original string - case 5`` () =
    let escape = escapeString Environment.NewLine "," true
    let splitdata = splitRowStr ',' escape
    match splitdata with 
    | None -> Assert.Fail()
    | Some items -> 
        Assert.AreEqual(1,items.Length)
        Assert.AreEqual(Environment.NewLine, items.[0])


[<Test; Category("CI")>]
let ``Escape then parse makes an original string - case 4`` () =
    let escape = escapeString null "," true
    let splitdata = splitRowStr ',' (escape + ",a")
    match splitdata with 
    | None -> Assert.Fail()
    | Some items -> 
        Assert.AreEqual(2,items.Length)
        Assert.AreEqual(null, items.[0])

[<Test; Category("CI")>]
let ``Simple comma delimiter test ``() =
     let splitdata = splitRowStr ',' "This,is,a,test,!"
     match splitdata with 
     | Some items -> items |> should equal [|"This";"is";"a";"test";"!"|]
     | None -> Assert.Fail()

[<Test; Category("CI")>]
let ``Comma delimiter test with spaces ``() =
     let splitdata = splitRowStr ',' "T h i s,i s,  a,t e s t,!"
     match splitdata with 
     | Some items -> items |> should equal [|"T h i s";"i s";"  a";"t e s t";"!"|]
     | None -> Assert.Fail()
    
[<Test; Category("CI")>]
let ``Comma delimiter test with nulls ``() =
     let splitdata = splitRowStr ',' "Hey,there,is,an,,empty,element"
     match splitdata with 
     | Some items -> items |> should equal [|"Hey";"there";"is";"an";null;"empty";"element"|]
     | None -> Assert.Fail()

[<Test; Category("CI")>]
let ``Comma delimiter test with quotes ``() =
     let splitdata = splitRowStr ',' "\"You shall not pass!\",Gandalf"
     match splitdata with 
     | Some items -> items |> should equal [|"You shall not pass!";"Gandalf"|]
     | None -> Assert.Fail()

[<Test; Category("CI")>]
let ``Comma delimiter test with multi-line quote ``() =
     let splitdata = splitRowStr ',' "\"You shall not pass!\nGo back to the shadow!\",Gandalf"
     match splitdata with 
     | Some items -> items |> should equal [|normalizeNL "You shall not pass!\nGo back to the shadow!";"Gandalf"|]
     | None -> Assert.Fail()

[<Test; Category("CI")>]
let ``Simple schema reading test`` () = 
    use reader = File.OpenText(@"tests\Simple schema reading test.csv")
    Implementation.Read ReadSettings.Default reader |> CompareTables  
        [|{Name = "1.0"; Type = ColumnType.String},upcast[||]
         ;{Name = "2.0"; Type = ColumnType.String},upcast[||]
         ;{Name = "3.0"; Type = ColumnType.String},upcast[||]|]

[<Test; Category("CI")>]
let ``Reading empty file`` () = 
    use r = asReader("")
    Implementation.Read ReadSettings.Default r |> CompareTables [||]

[<Test; Category("CI")>]
let ``Reading header-only file`` () = 
    use r = asReader("x")
    Implementation.Read ReadSettings.Default r |> CompareTables [|{Name = "x"; Type = ColumnType.String}, upcast Array.empty<string>|]

    
[<Test; Category("CI")>]
let ``One line of data test`` () = 
    let reader = File.OpenText(@"tests\One line of data test.csv")
    Implementation.Read ReadSettings.Default reader |> CompareTables 
        [|{Name = "Col1"; Type = ColumnType.Double},upcast[|1.0|]
         ;{Name = "Col2"; Type = ColumnType.Double},upcast[|2.0|]
         ;{Name = "Col3"; Type = ColumnType.Double},upcast[|3.0|]|]
[<Test; Category("CI")>]
let ``All types of data test`` () = 
    let reader = File.OpenText(@"tests\All types of data test.csv")
    let doubles = [|1.0;2.0|]
    let bools :bool[] = [|true;false|]
    let dates = [|new DateTime(1995, 4, 10); new DateTime(1995, 4, 11)|]
    let strings = [|"Hello! It's me";"GoodBye!"|]
    Implementation.Read ReadSettings.Default reader|> CompareTables  
        [|{Name = "Double Column"; Type = ColumnType.Double},upcast doubles
         ;{Name = "Bool Column"; Type = ColumnType.Boolean}, upcast bools
         ;{Name = "DateTime Column"; Type = ColumnType.DateTime},upcast dates
         ;{Name = "String Column"; Type = ColumnType.String},upcast strings|]

[<Test; Category("CI")>]
let ``Strings test 1`` () = 
    let reader = File.OpenText(@"tests\Strings test 1.csv")
    Implementation.Read ReadSettings.Default reader|> CompareTables  
        [|{Name = "Quotes"; Type = ColumnType.String}, upcast [|"Hello, world";"Hi"|]
         ;{Name = "Authors"; Type = ColumnType.String}, upcast [|"Any \"good\" programmer";"\"someone quoted\""|]|]

[<Test; Category("CI")>]
let ``import correct file``() =
    let reader = File.OpenText(@"tests\wheat.csv")
    let resultingdata = Implementation.Read ReadSettings.Default reader

    resultingdata.Length |> should equal 3

    let lons = (resultingdata.[0] |> snd) :?> ImmutableArray<double>
    Assert.AreEqual(lons.Length, 4)
    Assert.AreEqual(lons.[0], 111.5, 0.1)

    let lat = (resultingdata.[1] |> snd) :?> ImmutableArray<double>
    Assert.AreEqual(lat.Length, 4)
    Assert.AreEqual(lat.[0], 45.5, 0.1)

    let wheat = (resultingdata.[2] |> snd) :?> ImmutableArray<double>
    Assert.AreEqual(wheat.Length, 4)
    Assert.AreEqual(wheat.[0], 0.004388, 0.1)

 
[<Test; Category("CI")>]
let ``import file with different column types``() =
    let reader = File.OpenText(@"tests\typedColumns.csv")
    let resultingdata = Implementation.Read {ReadSettings.Default with Delimiter = Delimiter.Semicolon} reader

    resultingdata.Length |> should equal 4

    let ints = (resultingdata.[0] |> snd) :?> ImmutableArray<double>
    Assert.AreEqual(ints.Length, 19)
    Assert.AreEqual(ints.[0], 0)

    let floats = (resultingdata.[1] |> snd) :?> ImmutableArray<double>
    Assert.AreEqual(floats.Length, 19)
    Assert.AreEqual(floats.[0], 0.0, 0.1)
    
    let strings = (resultingdata.[2] |> snd) :?> ImmutableArray<string>
    Assert.AreEqual(strings.Length, 19)
    Assert.AreEqual(strings.[0], "string_0")

    let dates = (resultingdata.[3] |> snd) :?> ImmutableArray<DateTime>
    Assert.AreEqual(dates.Length, 19)
    Assert.AreEqual(dates.[0], System.DateTime(2014,10,15))


[<Angara.Data.TestsF.Common.Property; Category("CI")>]
let ``Column index converted to name and back gives an original index`` (index: int) =
    index >= 0 ==> lazy(
        let name = Helpers.indexToName index    
        let eval (c:char) = int(c) - int('A')
        let index2 =  name.ToCharArray() |> Array.fold (fun acc c -> (1 + acc) * 26 + (eval c)) -1
        System.Diagnostics.Trace.WriteLine(sprintf "%d -> %s -> %d" index name index2)

        not (String.IsNullOrWhiteSpace name) && index = index2)

[<Test; Category("CI")>]
let ``Column index to name`` () =
    Assert.Throws(typeof<System.ArgumentException>, fun () -> ignore (Helpers.indexToName -1)) |> ignore
    Assert.AreEqual("A", Helpers.indexToName 0)
    Assert.AreEqual("Z", Helpers.indexToName 25)
    Assert.AreEqual("AA", Helpers.indexToName 26)
    Assert.AreEqual("AC", Helpers.indexToName 28)
    Assert.AreEqual("BFI", Helpers.indexToName 1516)


[<Test; Category("CI")>]
let ``Read a table from a file with header by default``() =
    let table = Table.Load(@"tests\wheat.csv")

    table.Count |> should equal 3
    table.RowsCount |> should equal 4

    let lons = table.["lon"].Rows.AsReal
    Assert.AreEqual(lons.Length, 4)
    Assert.AreEqual(lons.[0], 111.5, 0.1)

    let lats = table.["lat"].Rows.AsReal
    Assert.AreEqual(lats.Length, 4)
    Assert.AreEqual(lats.[0], 45.5, 0.1)

    let wheat = table.["wheat"].Rows.AsReal
    Assert.AreEqual(wheat.Length, 4)
    Assert.AreEqual(wheat.[0], 0.004388, 0.1)

[<Test; Category("CI")>]
let ``Read a table from a file without header``() =
    let table = Table.Load(@"tests\wheat-noheader.csv", { ReadSettings.Default with HasHeader = false })

    table.Count |> should equal 3
    table.RowsCount |> should equal 4

    let lons = table.["A"].Rows.AsReal
    Assert.AreEqual(lons.Length, 4)
    Assert.AreEqual(lons.[0], 111.5, 0.1)

    let lats = table.["B"].Rows.AsReal
    Assert.AreEqual(lats.Length, 4)
    Assert.AreEqual(lats.[0], 45.5, 0.1)

    let wheat = table.["C"].Rows.AsReal
    Assert.AreEqual(wheat.Length, 4)
    Assert.AreEqual(wheat.[0], 0.004388, 0.1)

[<Test; Category("CI")>]
let ``Read a table from an empty file with a header``() =
    let table = Table.Load(@"tests\empty.csv", { ReadSettings.Default with HasHeader = true })
    table.Count |> should equal 0
    table.RowsCount |> should equal 0

[<Test; Category("CI")>]
let ``Read a table from an empty file without a header``() =
    let table = Table.Load(@"tests\empty.csv", { ReadSettings.Default with HasHeader = false })
    table.Count |> should equal 0
    table.RowsCount |> should equal 0
    
[<Test; Category("CI")>]
let ``Write without header really doesn't writes the header``() =
    let t = Table.OfColumns([ Column.Create ("lat", [|1.0;2.0;3.0|])
                              Column.Create ("lon", [|11.0;21.0;31.0|]) ])
    use ms = new MemoryStream()
    Table.Save(t, new StreamWriter(ms), { WriteSettings.Default with SaveHeader = false })
    ms.Position <- 0L

    let reader = new StreamReader(ms)
    let n = Seq.initInfinite(fun _ -> reader.ReadLine()) |> Seq.takeWhile(fun r -> not(String.IsNullOrWhiteSpace r)) |> Seq.toArray |> Array.length
    Assert.AreEqual(3, n)

    ms.Position <- 0L

    let t2 = Table.Load(reader, { ReadSettings.Default with HasHeader = false })
    Assert.AreEqual(t.Count, t2.Count, "columns count")
    Assert.AreEqual(t.["lat"].Rows.AsReal, t2.["A"].Rows.AsReal, "lat -> A")
    Assert.AreEqual(t.["lon"].Rows.AsReal, t2.["B"].Rows.AsReal, "lon -> B")