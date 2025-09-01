#r "nuget: ExcelProvider"
#r "nuget: FSharp.Data"
open FSharp.Interop.Excel
open FSharp.Data
open System



[<Literal>]
let ResolutionFolder = __SOURCE_DIRECTORY__

// Define the type provider for Excel
type PowerPricesExcel = ExcelFile<"powerprices.xlsx", ResolutionFolder=ResolutionFolder>

// Define a record type for each row
type PowerPrice = {
    DateAndTime: string
    Area: string
    Price: decimal
}

// Load the Excel file
let excel = new PowerPricesExcel()

// Map each row to the PowerPrice record
let allPrices =
    [ 
        for row in excel.Data do
        yield {
            DateAndTime = row.``Dato/klokkeslett``
            Area = "NO1"
            Price = (decimal)row.NO1
        }
        yield {
            DateAndTime = row.``Dato/klokkeslett``
            Area = "NO2"
            Price = (decimal)row.NO2
        }
        yield {
            DateAndTime = row.``Dato/klokkeslett``
            Area = "NO3"
            Price = (decimal)row.NO3
        }
        yield { 
            DateAndTime = row.``Dato/klokkeslett``
            Area = "NO4"
            Price = (decimal)row.NO4
        }
    ] 
    |> List.groupBy (fun p -> p.Area, p.DateAndTime)
    |> Map.ofList
    |> Map.map (fun _ v -> v |> List.head)


// Make sure the files are in right "format"
let replace (from:string) (toReplace:string) (str:string) = str.Replace(from, toReplace)
let write (f:string) (str:string) =
    IO.File.WriteAllText(f, str)

let readReplaceWrite (f:string) = 
    f
    |> IO.File.ReadAllText 
    |> replace "," "." 
    |> write f

let files = IO.Directory.GetFiles(ResolutionFolder, "*.csv")

files |> Array.iter (readReplaceWrite)


// Read the consumption CSV file and parse them

let toDate str = DateTime.ParseExact(str, "dd.MM.yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture)

type Consumption = 
    { From: DateTime
      To: DateTime
      Consumption: decimal }

type Useage = CsvProvider<"Måleverdier_707057500017399037_CONSUMPTION_2024-01.csv", ResolutionFolder=ResolutionFolder, Separators=";">

let useage = Useage.Load("Måleverdier_707057500017399037_CONSUMPTION_2024-01.csv")

let g = useage.Rows |> Seq.head

let fromUseageToConsumption (useage:Useage) = 
    useage.Rows 
    |> Seq.map (fun r -> 
        { From = r.Item1 |> toDate
          To = r.Item2 |> toDate
          Consumption = r.Item3 })
    |> Seq.toList

// 2024-01-01 Kl. 08-09"
let makeTimeKey (t1:DateTime) (t2:DateTime) = 
    t1.ToString("yyyy-MM-dd") + " Kl. " + t1.ToString("HH") + "-" + t2.ToString("HH")

let makeSetKey area (t1:DateTime) (t2:DateTime) = 
    area, makeTimeKey t1 t2


let consumptionWithPrice consumption =
    consumption
    |> List.map (fun c ->
        let key = makeSetKey "NO1" c.From c.To
        let price = allPrices.[key].Price
        let price90 = 
            if price > 0.9375M then 
                price - (price - 0.9375M) * 0.9M
            else
                price
        let price50 = 0.5M
        
        price*c.Consumption, price90*c.Consumption, price50 * c.Consumption)
    |> List.fold(fun (sf, s90, s50) (pf, p90, p50) -> sf+pf, s90+p90,s50 + p50) (0M, 0M, 0M)

files|> Array.sort |> Array.iter (fun f -> 
    let useage = Useage.Load(f)
    let total, total90, total50 = useage |> fromUseageToConsumption |> consumptionWithPrice
    printfn "%s: Total: %.2f, Total with 90% discount: %.2f, Total with max50: %.2f, 90<50: %A" f total total90 total50 (total90 < total50)
    )   