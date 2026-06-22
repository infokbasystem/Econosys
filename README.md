# Econosys - Fullstack Solution

Detta är ett monorepo som innehåller både backend och frontend för Netpack-applikationen.

## 🏗 Projektstruktur

- **/Econosys.Api**: Backend byggd med .NET 9. Innehåller REST API-logik och databasintegrationer.
- **/Econosys.Web**: Frontend byggd med React (JavaScript).

## 🚀 Komma igång

### Förutsättningar
- [.NET 9 SDK](https://microsoft.com)
- [Node.js](https://nodejs.org) (senaste LTS rekommenderas)
- [Visual Studio Code](https://visualstudio.com)

### Köra projektet i VS Code
Det enklaste sättet att utveckla är att använda de förkonfigurerade debug-inställningarna:

1. Öppna mappen `Econosys` i VS Code.
2. Gå till fliken **Run and Debug** (Ctrl+Shift+D).
3. Välj **"Kör hela Econosys"** i rullistan.
4. Tryck **F5**.

Detta kommer att:
- Bygga och starta .NET-API:et (öppnar Swagger på `/swagger`).
- Starta React-utvecklingsservern.
- Öppna Chrome automatiskt mot frontend.

## 🛠 Utveckling

### Backend (API)
- Projektfil: `Econosys.Api/Econosys.Api.csproj`
- Solution: `Econosys.sln`
- Dokumentation: Nås via `/swagger` vid körning.
- Tester: Kan köras via `dotnet test`.

### Frontend (Web)
- Baserad på React.
- Hanteras via npm inuti mappen `Econosys.Web`.
- För att installera paket: `cd Econosys.Web && npm install`.

## 🤖 AI Context (för Claude/GitHub Copilot)
Detta projekt är ett monorepo. Vid ÄNDRINGAR som rör datamodeller, se till att uppdatera både C#-klasserna i `Econosys.Api` och motsvarande API-anrop/logik i `Econosys.Web`.
