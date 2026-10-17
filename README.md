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

### Pall/packning i kalkyler
- Alternativen hämtas med `/api/palletformats/searchwithprices` för vald kund och leverantör. Endast format kopplade till båda visas.
- Listan sorteras efter palltypens sorteringsnummer, formatets sorteringsnummer och namn. Inaktiva format eller palltyper kan inte väljas.
- Kundens och leverantörens registrerade pallpriser följer med alternativen; sparade kalkylvärden ändras inte när listan laddas.
- Med automatisk packinstruktion aktiverad uppdateras texten vid ändring av pallmått, antal/pall, stapling, buntning eller ytterförpackning, samt vid byte av pallformat. Texten följer legacy-appens format. När automatiken stängs av behålls texten för manuell redigering.

### Uppdatera och godkänn kalkylkostnader
- Packning, Fraktberäkning, Övriga kostnader och Lagringskostnader har en gemensam länkåtgärd per sektion: **Uppdatera och godkänn**. Åtgärden och godkännandestatus visas bredvid rubriken även när sektionen är stängd.
- Åtgärden uppdaterar registervärden och applicerar beräknade kostnader på aktiva kalkylrader innan sektionen markeras för godkännande. Arkiverade rader ändras inte. Manuella fraktpriser används när automatiska priser är avstängda; deaktiverad frakt rensas från kalkylraderna.
- Godkännanden sparas först med **Spara**. Fram till dess visas **Godkänns när kalkylen sparas**; efter sparande visas tidpunkt och användare från servern. Vanliga sparningar behåller tidigare godkännanden.
- Verktygsfältets **Uppdatera och godkänn alla** kör samma åtgärder för alla fyra sektioner. Frakten uppdateras först så packningen använder aktuella pallantal. Alla resultat appliceras tillsammans när samtliga steg lyckas; ett fel avbryter utan att applicera delresultat. Även dessa godkännanden sparas först med **Spara**.
- Misslyckade registeruppslag eller blockerande beräkningsvarningar ger ett felmeddelande utan att sektionen godkänns. Om kalkylen ändras under ett uppslag behöver åtgärden köras igen.

### Fraktlägen i kalkyler
- **Deaktivera frakt** stänger av båda automatikerna och rensar fraktkostnader på aktiva rader direkt, inklusive frakten i kalkylraderna. Resultatet markeras för godkännande vid nästa **Spara**. Pallantal kan fortfarande redigeras. Återaktivering återställer inte gamla priser eller automatiker.
- **Automatisk fraktberäkning** beräknar pallantal, pallplatser, pall/bil och radens pallfaktor från pallmått, stapling och pallfaktorregistret. Utan automatiken anges pallantal och antal bilar manuellt; pall/bil nollställs.
- **Automatiskt fraktpris** är oberoende av automatisk fraktberäkning. Påslaget och transportpriser hämtas från vald fabrik/leveransdestination; standardfabrik och standardadress väljs när sådana finns. Utan prisautomatiken rensas destinationsval och påslag, medan transportpriser kan redigeras manuellt. Lossningskostnader kan redigeras i båda aktiva lägena.
- Pallfaktoruppslag använder samma dimensionsordning och intervallgränser som legacy. Saknade eller ogiltiga faktorer visas som fel och blockerar fraktgodkännande. Arkiverade rader påverkas inte av lägesändringar.

## 🤖 AI Context (för Claude/GitHub Copilot)
Detta projekt är ett monorepo. Vid ÄNDRINGAR som rör datamodeller, se till att uppdatera både C#-klasserna i `Econosys.Api` och motsvarande API-anrop/logik i `Econosys.Web`.
