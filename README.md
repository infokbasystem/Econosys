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

### E-postmottagare
- Maildialogen hämtar nya mottagarlistor för varje dokument via `/api/email/recipients/{itemType}/{itemId}`. Kontaktpersonernas dokumentflaggor styr vilka som är förvalda; övriga kontakter visas omarkerade med befattning.
- Förfrågan och beställning använder leverantörskontakter; affärsförslag, ordererkännande och faktura använder kundkontakter. Transportorder följer vald transportör, leverantör eller lager; avropsbekräftelse använder kundkontakter.
- Kopia till följer legacy: inloggad användare för förfrågan/affärsförslag/beställning/ordererkännande, `transport@econopack.se` för avrop/transportorder, och `customerinvoice@econopack.se` samt `skickadepdffakturor@econopack.se` för faktura. Kopior och nya tomma rader är markerade som standard.
- Om mottagarlistorna inte kan laddas visas ett fel och sändning blockeras; stäng och öppna dialogen för att försöka igen.

### Gemensam PDF-layout
- Alla genererade PDF:er har 8,5 mm sidmarginaler till vänster och höger och 15 mm marginaler upptill och nedtill.

### Förfrågan-PDF
- Mått visas med enheten **mm** efter formatet om enheten inte redan finns.
- En svart avdelare skiljer rubriken **Tillägg** från kostnadsraderna.
- Leverans- och betalningsvillkoren har extra utrymme ovanför sidfoten.

### Affärsförslag-PDF
- **Tillägg** följer den kostnadsversion som visas under **Orderkostnader**. Endast debiterbara kostnader som hör till affärsförslaget och den valda versionen visas; utskriftsflaggan påverkar inte urvalet. Utan vald beställningsversion används de ej beställningskopplade kostnaderna.
- Pallkostnaden visar antal × styckpris när båda är kända och skilda från noll. Den fristående verktygsnotisen visas inte på affärsförslaget.
- Momsnotisen och eventuell giltighetstext visas direkt under pristabellen (**Antal / Pris / Notering**).
- Leverans- och betalningsvillkoren placeras 2 cm högre utan att sidfoten flyttas.

### Beställning-PDF
- Producentens adress visas till vänster och leveransadressen till höger. Båda hämtas från uppgifterna sparade på beställningen och visas oberoende av godsmärkningsvalen, enligt legacy.
- Pristabellens färgade rubrik har rundade ytterhörn.

### Ordererkännande-PDF
- Pristabellens färgade rubrik har rundade ytterhörn.
- **Tillägg** visar debiterbara kostnader som är markerade för utskrift på ordererkännandet. Belopp visas utan decimaler, och nollpris använder texten för okänt pris. DMT visas utan belopp.
- Palltillägget visas när både kundordern och det länkade affärsförslaget har palltillägg. Antal × styckpris visas när båda är skilda från noll. Verktygens lagringstid visas bara när en debiterbar kostnad är markerad för den texten.
- Leverans- och betalningsvillkoren använder hela innehållsbredden och placeras 2 cm högre, utan avdelare ovanför.

### Faktura-PDF
- Vänstra sidfoten hämtar adress, telefon, e-post, webbplats och **F-skattesedeltext** från **Inställningar → Företagsinfo**. Internationellt telefonnummer används om det finns, annars det nationella. F-skattesedeltexten visas i kursiv stil och utelämnas om fältet är tomt.
- Hela sidfotens text visas i mörkgrått.
- Fakturaradernas färgade kolumnrubriker har rundade ytterhörn.
- Fakturadatum, förfallodag, fakturarubrik och fakturanummer är högerjusterade.
- Summeringens belopp är högerjusterade mot fakturaradernas **Total**-kolumn.
- När kundens **Utskr.scanning** är aktiverat genereras fakturan i svartvitt med svart logotyp, utan vattenstämpel eller färgad tabellrubrik.

### Fakturautkast från leveranser och orderkostnader
- Fakturarader kan sorteras genom att dra handtaget längst till höger till en annan rad, eller med **Alt + pil upp/ned** när handtaget har fokus. Raderna glider till sin nya plats under dragningen; avbruten dragning återställer ordningen. Animeringen respekterar inställningen för minskad rörelse. Papperskorgen bredvid handtaget tar bort raden. Ordningen och radnumren sparas med fakturan. Moms-/öresrader ligger kvar sist; omflyttning ändrar inte belopp eller lagervärdering.
- Om de valda leveranserna och orderkostnaderna hör till exakt ett ordererkännande fylls **Vår referens**, **Ert ordernr.** och **Vårt ordernr.** i från ordererkännandets referens, kundens ordernummer respektive ordererkännandets nummer.
- Vid flera eller inga underliggande ordererkännanden lämnas dessa fält tomma. **Er referens** hämtas fortsatt från kunden. Utkastets värden kan ändras manuellt före sparande; befintliga fakturor ändras inte.
- Nya produktraders text följer legacy: ordererkännandets produkttext, **Ert ordernr:**, **Vårt ordernr:**, eventuellt **Avrop** och **Vikt:** visas på separata rader. Avropstexten hämtas från leveransens sparade avropsnummer; rubriken översätts till kundens språk när översättning finns.
- Vid direktleverans används transportorderradens kundordernummer om exakt en matchande rad finns och numret är ifyllt, annars ordererkännandets. Vid lagerleverans används avropets notering om exakt ett avrop är kopplat, även om noteringen är tom; annars används ordererkännandets kundordernummer. En slattpall vars huvudleverans ingår i urvalet får tomt värde efter **Ert ordernr:**.
- Vikttexten beräknas per leverans från produktens nettovikt per 1 000 och leveransens antal, avrundad till hela kg med svensk tusentalsavgränsning. Saknade talvärden räknas som noll; ofullständiga produktkopplingar loggas och tillgänglig text behålls. Slattpallens vikt slås inte ihop med huvudleveransens. Textreglerna ändrar inte det numeriska viktfältet eller redan sparade texter.
- Orderkostnader följer legacy: mängdkostnader använder kostnadens antal utan orderenhetens divisor; fasta kostnader debiteras en gång och lagrar antal `-1` (visas som tomt, låst antal). Saknat försäljningspris ger fel och ersätts inte med inköpspris. Med leveranser används översatt kostnadsnamn följt av `, order <ordernr>`; rena kostnadsfakturor använder originalnamnet, produkt/order och **Ert onr:** på tre rader.
- Debiterbara, ännu ej fakturerade ordinarie kostnader tas automatiskt med för valda order. Separata/omgående separata kostnader skapas utan leveranser. DMT-procent och fast DMT får återkomma även efter tidigare fakturering. Procentpriset räknas från vald kalkylrads frakt × upplaga / orderenhet × DMT-procent; fast DMT använder sparat försäljningspris. Till skillnad från legacy ändrar utkastet inte orderkostnadens sparade pris.
- Pallrader (typ 107) använder leveransens pallantal, orderns pallpris, pallformatets översättning och kundens/marknadens intäktskonto. För kunder med separat pallfakturering visas valbara underlagsrader **Pallar redan lev** och **Pallar ej lev**, som i legacy, i både kund- och listvyn. **Skapa faktura** väljer läge automatiskt från markerad rad; ingen separat lägesväljare används. Pallrader får inte blandas med andra underlag eller med den andra perioden. Utkastet innehåller endast pallrader från leveranser med status levererad, positivt pallantal och datum i vald period. Även kunder vars produkter redan fakturerats visas när separata pallar återstår.
- Debiterbar avropsfrakt (typ 108, **Frakt**) tas med en gång per avrop. Nya fraktrader lagrar `Freight:<avrops-id>` för att förhindra dubbeldebitering även över flera fakturor; äldre fraktrader utan avrops-id kan inte identifieras retroaktivt. Pall-/kostnadsstatus markeras först vid sparande och återställs när sista kopplade raden tas bort. Sparande kontrollerar redan fakturerade underlag och sker transaktionellt.
- Momsen följer kundens momsflagga och procentsats (25 % om moms är aktiv men procentsats saknas/är noll), med legacy-bankavrundning till två decimaler. Moms/öresutjämning använder decimalberäkning på servern och motsvarande avrundning i vyn så binära flyttalsfel vid halva ören inte ändrar beloppen vid redigering. **Öresutjämning** avrundar hela summan till hel krona, halva kronor bort från noll även för negativa fakturor. Avsiktlig rättning av legacy: även momsfria och icke momsgrundande belopp ingår i öresutjämningen. Redigering i fakturavyn använder samma moms-/kostnadsregler; att enbart öppna en befintlig faktura ändrar inte dess belopp.
- Bokföringsraderna är lageruttag och motbokad **Lagerförändring**, grupperade per kontonummer, inte kopior av försäljningsraderna. Värdet beräknas från lagerproduktens antal, beställningens inköpspris, inköpsvalutans lagervärderingskurs och beställningens enhet. Lagerkonto används om det finns, annars standardkonto 18; motkonto är 19. Lager utan lagervärde undantas. Saknade nödvändiga värderingsuppgifter ger tydliga fel. Avsiktlig rättning av legacy: pallrader gör inte ett andra lageruttag. Förhandsvisning uppdateras efter ändrat antal/borttagen lagerprodukt och servern räknar om vid sparande.

### Automatiska frontenduppdateringar
- Varje frontendbygge genererar ett unikt build-ID och `dist/version.json`. ID:t visas diskret längst ned till höger och döljs vid utskrift.
- Produktionsappen kontrollerar versionen vid start, var 60:e sekund och när fliken åter blir synlig eller får fokus. Utvecklingsservern och testbyggen laddas inte om automatiskt.
- Vid en ny driftsättning laddas sidan om automatiskt, men först när osparade ändringar är sparade eller förkastade, öppna redigeringsdialoger är stängda och API-anrop är avslutade. Aktiva inmatningsfält skyddas också tills fokus lämnar fältet.
- En pågående leveransregistrering skyddas tills den har sparats eller sidan lämnas. Uppdateringar ändrar inte befintliga navigeringsvarningar.
- Frontend publiceras till Azure Static Web Apps via `deploy-frontend.sh`. `public/staticwebapp.config.json` stänger av cache för HTML och versionsfilen; hashade filer under `/assets/` behåller lång cachetid. Versionsfilen undantas från SPA-fallback.
- Flikar som redan kör kod utan uppdateringskontrollen behöver laddas om en gång efter den första publiceringen. Om gammal HTML fortfarande levereras förhindras upprepade automatiska omladdningar; appen visar då ett meddelande om manuell omladdning.

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

### PPWR för produkter
- Produktens PPWR-knapp visar en tom ring tills en PPWR-uppdatering har registrerats, därefter en bock.
- Produktsökningens **PPWR**-kolumn ersätter produktkoden och visar en turkos **PPWR**-pill när uppdateringsdatum finns. Övriga produkter har en tom cell. Kolumnen kan sorteras på uppdateringsdatum.

### Orderflöde i kalkyler
- **Arkiverad** under senast redigerad-informationen markerar hela kalkylen som arkiverad. Statusen sparas med **Spara** och är oberoende av arkiverade kalkylrader. Nya och befintliga kalkyler är inte arkiverade som standard; EF-migreringen `AddCalculationIsArchived` lägger till kolumnen.
- Orderträden i produkt, förfrågan, affärsförslag, beställning och kundorder döljer arkiverade kalkyler med underliggande dokument som standard. **Visa även arkiverade** visar även dessa grenar; **Dölj arkiverade** döljer dem igen.
- **Ny förfrågan**, **Nytt affärsförslag** och **Ny beställning** öppnas i samma webbläsarflik som kalkylen. Kalkylkopplingen följer med och befintlig varning för osparade ändringar gäller vid navigeringen.
- Valdialogerna för **Ny beställning** använder appens gemensamma modalstil: ljusgul bakgrund, centrerad rubrik, orange avbryt-/tillbakaknappar och gröna åtgärdsknappar.
- Osparade beställningar visar **Orderkostnader** för redigering av utkast och en tom **Orderhistorik**. Historik hämtas först när beställningen har sparats.
- Nya beställningar får `SupplierOrderNr` enligt legacy: kundens `OrderNrPrefix` följt av beställningens databas-ID. Numret används i rubriker, tabeller, rapporter och utskrifter; länkar och relationer använder fortfarande ID. Befintliga nummer bevaras, och saknade nummer fylls i vid sparande.
- Nya ordererkännanden kopplade till en beställning får `strCustomerOrder` (`customerOrderNr` i API) från beställningens `SupplierOrderNr`, enligt legacy. Det första får samma nummer, följande får suffix `-2`, `-3` osv. Numret tilldelas av API vid sparande och visas i rubriker, navigation, listor och rapporter; länkar och relationer använder fortfarande databas-ID. Befintliga nummer räknas inte om. Headersökningen stöder även ordernummer med bokstäver och suffix (minst fem tecken).
- Fältet **Kunds onr** på ordererkännandet läser och sparar `strYourOrderNr` (`yourOrderNr` i API), separat från ordererkännandets nummer. Vid skapande från beställning hämtas startvärdet från beställningens kundordernummer enligt legacy.
- **Nytt ordererkännande** från en beställning visar beställningens befintliga orderkostnader i utkastet. Vid sparande kopplas samma kostnadsposter till ordererkännandet utan kopiering; kopplingen till beställningen och kostnadernas övriga metadata bevaras.
- **Vår referens** på ett nytt ordererkännande från en beställning hämtas från kundens ansvariga säljare, inte från affärsförslagets eller beställningens referens. Saknas ansvarig säljare lämnas fältet tomt. Referensen kan ändras manuellt; befintliga ordererkännanden ändras inte.
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
