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

### Tangentbordsnavigering i kalkyltabeller
- I kalkylrader, fraktberäkning, övriga kostnader och lagringskostnader flyttar **Pil upp/ned** fokus inom samma kolumn. Inaktiverade, skrivskyddade och arkiverade rader hoppas över; navigeringen stannar vid första/sista redigerbara raden.
- **Enter** flyttar framåt som **Tab**, även ut ur tabellen. **Skift+Enter** flyttar bakåt. Vanlig **Tab/Skift+Tab** fungerar som tidigare.
- Att bara flytta fokus mellan talfält ändrar inte sparade decimaler och startar ingen fraktuppdatering. Fraktpriser hämtas automatiskt först när relevanta indata ändras.
- Reglerna gäller även listval, kryssrutor och radknappar. Använd mus eller **mellanslag** för att aktivera dessa; Enter navigerar i stället för att lägga till, ta bort eller ändra en rad.

### Redigera kalkylrad från historik

- Sista kolumnen i kalkylsidans **Historik** öppnar en modal för beställningens valda kalkylrad (`lngSelectedCalculationRow_ID` på beställningen). Saknas kopplingen är knappen inaktiverad.
- Modalen visar samtliga lagrade affärsfält på kalkylraden, även arkiverade frakt-, pall-, lager-, valuta- och prislistvärden. Identitet, företag och kalkylkoppling är skrivskyddade; underliggande kostnadsrader ändras inte.
- **Spara** ändrar endast kalkylraden, utan automatisk omberäkning. Beställningens och kundorderns priser, upplagor och andra värden ändras inte. Historikens orderbaserade kolumner behåller därför ordervärdena; varningsikonen visar eventuella prisskillnader.
- Historiken laddas om när modalen stängs, även med **Avbryt** eller Escape. Osparade ändringar på själva kalkylsidan bevaras.
- API: `GET` och `PUT /api/supplierorders/{id}/calculation-row`. Sparandet kräver `expectedRowId` och ett komplett `values`-objekt från läsningen; ändrad radkoppling ger 409. Arkiverade rader kan redigeras här, men är fortsatt skyddade i den vanliga kalkylredigeraren.

### Orderflöde i kalkyler
- **Arkiverad** under senast redigerad-informationen markerar hela kalkylen som arkiverad. Statusen sparas med **Spara** och är oberoende av arkiverade kalkylrader. Nya och befintliga kalkyler är inte arkiverade som standard; EF-migreringen `AddCalculationIsArchived` lägger till kolumnen.
- Orderträden i produkt, förfrågan, affärsförslag, beställning och kundorder döljer arkiverade kalkyler med underliggande dokument som standard. **Visa även arkiverade** visar även dessa grenar; **Dölj arkiverade** döljer dem igen.
- **Ny förfrågan**, **Nytt affärsförslag** och **Ny beställning** öppnas i samma webbläsarflik som kalkylen. Kalkylkopplingen följer med och befintlig varning för osparade ändringar gäller vid navigeringen.
- Valdialogerna för **Ny beställning** använder appens gemensamma modalstil: ljusgul bakgrund, centrerad rubrik, orange avbryt-/tillbakaknappar och gröna åtgärdsknappar.
- Osparade beställningar visar **Orderkostnader** för redigering av utkast och en tom **Orderhistorik**. Historik hämtas först när beställningen har sparats.
- **Nytt ordererkännande** från en beställning visar beställningens befintliga orderkostnader i utkastet. Vid sparande kopplas samma kostnadsposter till ordererkännandet utan kopiering; kopplingen till beställningen och kostnadernas övriga metadata bevaras.
- Ordererkännandets pallformat och debiteringsflagga följer med från beställningen. Pallpriset hämtas från det kopplade affärsförslaget och pallantalet börjar tomt enligt legacy. Antal och pris bevaras vid sparande; byte av pallformat använder kundens registrerade pallpris.
- Informationspanelen använder grupperad navigation enligt legacy: produkter, aktuell kalkyl, förfrågningar, affärsförslag, beställningar och ordererkännanden. Tomma grupper döljs och aktuell grupp markeras i rött.
- Raderna länkar till respektive post och visar namn, dokumentnummer och datum i formatet `yyMMdd`. Kalkylen visar redigeringsdatum; dokumenten visar skapandedatum.
- Endast den senaste förfrågan och det senaste affärsförslaget samt åtta senaste beställningarna/ordererkännandena visas först. Om fler finns visar **Visa fler** hela gruppen; **Visa färre** fäller ihop den igen.
- `/api/ordernavigation/calculation/{id}/flow` hämtar endast vald kalkyl och dess relaterade dokument, inte andra kalkyler för samma produkt. Navigationen uppdateras efter sparande och när fönstret återfår fokus. Befintliga nästlade orderträd på andra sidor påverkas inte.

### Förfrågan från kalkyl
- **Ny förfrågan** hämtar ett osparat utkast via `/api/inquiries/from-calculation/{id}`. Utkastet använder kalkylens sparade värden och skapar inga databasposter förrän **Spara** väljs.
- Leverantör, referenser, villkor, språk, FSC, kund, leveransadress och svarstid fylls i enligt legacy. Vid automatiskt fraktpris används kalkylens leveransadress, annars kundens standardadress.
- Produkttexter översätts till leverantörens språk när översättning finns. Registrerat format prioriteras framför mått, och lackuppgifter inkluderas i färgtexten.
- Endast aktiva kalkylrader följer med. Borttagna utkast-rader förblir borttagna vid sparande; leverantörsmottagaren och kvarvarande rader sparas tillsammans med förfrågan.

### Affärsförslag från kalkyl
- **Nytt affärsförslag** öppnar en modal där en befintlig förfrågan väljs. Endast kalkylens förfrågningar visas. **Skapa ny automatiskt** skapar och sparar i stället en förfrågan med kalkylens legacy-standardvärden, markerar den som automatiskt skapad och använder den för affärsförslaget. Alternativet finns även när kalkylen saknar förfrågningar.
- Modalen använder samma utseende som övriga komponentmodaler. Under automatiskt skapande blockeras upprepade klick och stängning; fel visas i modalen. Val av befintlig förfrågan skapar inga poster. Affärsförslaget förblir osparat tills **Spara** väljs.
- Utkastet hämtas via `/api/quotations/from-calculation/{id}?inquiryId={inquiryId}` utan att spara. Kalkylens och förfrågans kund måste vara samma. Aktiva kalkylrader utan påslag blockerar skapandet och visar ett felmeddelande.
- Kundens faktura- och leveransuppgifter, referenser, betalnings- och leveransvillkor, standardmeddelande, produkttexter, valutor, enhet och pallpris följer med. Leverantör och FSC kommer från vald förfrågan. Material och konstruktion översätts till kundens språk när översättning finns.
- Endast aktiva kalkylrader används; försäljningspriser avrundas enligt kalkylens försäljningsdecimaler, annars enhetens decimaler. **Spara** bevarar kopplingarna och skapar endast de utkast-rader som finns kvar.

### Uppdatera och godkänn kalkylkostnader
- Packning, Fraktberäkning, Övriga kostnader och Lagringskostnader har en gemensam länkåtgärd per sektion: **Uppdatera och godkänn**. Åtgärden och godkännandestatus visas bredvid rubriken även när sektionen är stängd.
- Åtgärden uppdaterar registervärden och applicerar beräknade kostnader på aktiva kalkylrader innan sektionen markeras för godkännande. Arkiverade rader ändras inte. Manuella fraktpriser används när automatiska priser är avstängda; deaktiverad frakt rensas från kalkylraderna.
- Godkännanden sparas först med **Spara**. Fram till dess visas **Godkänns när kalkylen sparas**; efter sparande visas tidpunkt och användare från servern. Vanliga sparningar behåller tidigare godkännanden.
- Verktygsfältets **Uppdatera och godkänn alla** kör samma åtgärder för alla fyra sektioner. Frakten uppdateras först så packningen använder aktuella pallantal. Alla resultat appliceras tillsammans när samtliga steg lyckas; ett fel avbryter utan att applicera delresultat. Även dessa godkännanden sparas först med **Spara**.
- Misslyckade registeruppslag eller blockerande beräkningsvarningar ger ett felmeddelande utan att sektionen godkänns. Om kalkylen ändras under ett uppslag behöver åtgärden köras igen.

### Fraktlägen i kalkyler
- **Ant pall** visas även direkt efter **Upplaga** i kalkylraderna, med samma värde och redigeringsregler som i fraktberäkningen. Knapparna **+** och **−** i kolumnen justerar upplagan till närmaste hela pall uppåt/nedåt (en hel pall om upplagan redan motsvarar hela pallar), precis som i fraktsektionen.
- När kalkylen öppnas och efter lyckat sparande visas en fraktvarning om leverantören har **Econopac trp.ansv.** och **Automatisk fraktberäkning** eller **Automatiskt fraktpris** är avstängt. Varningen anger vilka inställningar som är avstängda och ändrar inga värden.
- **Deaktivera frakt** stänger av båda automatikerna och rensar fraktkostnader på aktiva rader direkt, inklusive frakten i kalkylraderna. Resultatet markeras för godkännande vid nästa **Spara**. Pallantal kan fortfarande redigeras. Återaktivering återställer inte gamla priser eller automatiker.
- **Automatisk fraktberäkning** beräknar pallantal, pallplatser, pall/bil och radens pallfaktor från pallmått, stapling och pallfaktorregistret. Utan automatiken anges pallantal och antal bilar manuellt; pall/bil nollställs.
- **Automatiskt fraktpris** är oberoende av automatisk fraktberäkning. Påslaget och transportpriser hämtas från vald fabrik/leveransdestination; standardfabrik och standardadress väljs när sådana finns. Utan prisautomatiken rensas destinationsval och påslag, medan transportpriser kan redigeras manuellt. Lossningskostnader kan redigeras i båda aktiva lägena.
- Pallfaktoruppslag använder samma dimensionsordning och intervallgränser som legacy. Saknade eller ogiltiga faktorer visas som fel och blockerar fraktgodkännande. Arkiverade rader påverkas inte av lägesändringar.

## 🤖 AI Context (för Claude/GitHub Copilot)
Detta projekt är ett monorepo. Vid ÄNDRINGAR som rör datamodeller, se till att uppdatera både C#-klasserna i `Econosys.Api` och motsvarande API-anrop/logik i `Econosys.Web`.
