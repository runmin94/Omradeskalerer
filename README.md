# Områdeskalerer

En brukervennlig Windows-app for lineær skalering fra ett tallområde til et annet.

## Funksjoner

- Vanlige forhåndsvalg for bit- og analoge områder
- Standard skalering fra `0–32767` til `4–20`
- Interaktiv slider
- Bitområder bruker heltall
- Analoge områder som `4–20` bruker desimaler
- Bytt områder uten å flytte sliderens posisjon
- Kopier skalert resultat

## Bygg og kjør

Krever .NET 10 SDK på Windows.

```powershell
dotnet run
```

Publiser som en selvstendig 64-biters Windows-app:

```powershell
dotnet publish --configuration Release --output .\publish
```

Den kjørbare filen opprettes som `publish\Omradeskalerer.exe`.

## Skaleringsformel

```text
ut_min + ((verdi - inn_min) / (inn_maks - inn_min)) × (ut_maks - ut_min)
```
