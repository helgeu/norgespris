#!/usr/bin/env pwsh
Invoke-WebRequest -Uri "https://strom-api.forbrukerradet.no/spotprice/hourly/export/2024-01-01?to=2025-09-01&format=xlsx" -OutFile "powerprices.xlsx"