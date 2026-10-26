# Desktop Hide

Desktop Hide je lehká aplikace pro Windows 10/11 od Ajous Production. Skrývá a obnovuje ikony plochy globální klávesovou zkratkou a nabízí ruční/automatický Saver Mode s plynulým ztmavením, hudbou a rotací vlastních tapet. Nevyžaduje administrátorská práva ani síťové připojení při běžném používání.

## Instalace pro uživatele

Stáhněte a spusťte `DesktopHideSetup.msi`. Instalátor přidá aplikaci do nabídky Start a nainstaluje ji do `%LOCALAPPDATA%\Programs\Desktop Hide`. Je to self-contained .NET aplikace: cílový počítač nemusí stahovat ani instalovat .NET runtime. Při přihlášení do Windows se spustí na pozadí; nastavení lze kdykoli změnit v aplikaci. Odinstalování je dostupné v aplikaci tlačítkem **Smazat aplikaci…** nebo v Nastavení Windows → Aplikace.

## Sestavení ze zdrojů

Požadavky: Windows, .NET 8 SDK a WiX Toolset 6.

```powershell
dotnet publish .\DesktopHide.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o .\publish-v1.1
wix build .\installer\DesktopHide.wxs -arch x64 -d PublishDir="$(Resolve-Path .\publish-v1.1)" -o .\DesktopHideSetup.msi
```

MSI je per-user instalátor a nepožaduje elevaci. Build x64 je určen pro běžná 64bitová PC s Windows.

## Používání

Výchozí zkratka pro ikony je `Pause`, pro Saver Mode `Ctrl + Alt + S`. Obě lze nastavit tlačítkem **Nastavit zkratku**. Podporovány jsou Pause, F1–F11 samostatně a kombinace Ctrl, Alt, Shift nebo Win s další klávesou. F12 je vyhrazeno debuggeru; pokud zkratku používá jiná aplikace nebo Windows, zobrazí se chybová zpráva. Nastavení je v `%APPDATA%\DesktopHide\settings.json`.

Křížek hlavního okna okno pouze skryje; aplikace zůstane dostupná v systémové oblasti vedle hodin. Dvojklik na ikonu otevře nastavení. Nabídka po pravém kliknutí zobrazuje aktuální stav a nabízí skrýt/zobrazit ikony, zapnout Saver Mode, změnit spuštění s Windows, otevřít nastavení nebo aplikaci ukončit.

## Saver Mode

Saver Mode lze zapnout tlačítkem v okně, nabídkou ikony vedle hodin nebo globální zkratkou. Automatické spuštění po nečinnosti má samostatný přepínač. Volba **Pozastavit automatický spořič při přehrávání zvuku** (ve výchozím stavu zapnutá) kontroluje aktivní audio relace Windows a odloží automatické spuštění, když právě hraje YouTube, hudební přehrávač nebo jiná aplikace. Ruční spuštění zůstává dostupné.

Po spuštění se hlavní okno Desktop Hide skryje do tray, běžná viditelná okna se minimalizují, ikony plochy se skryjí a kurzor zmizí. Saver Mode zůstává aktivní při klávesách a drobném chvění myši; kurzor musí přesunout nejméně 48 pixelů od místa vstupu, aby se okna obnovila. Lze nastavit plynulé ztmavení 0–100 %, přehrávání hudby a hlasitost. Vybráním složky s obrázky PNG/JPG/BMP/GIF se tapety ve spořiči střídají dle zvoleného intervalu. Po ukončení se obnoví okna minimalizovaná aplikací.

Minimalizace oken jiných programů je omezená veřejnými Windows API; aplikace v exkluzivním režimu, hry nebo okna, která minimalizaci ignorují, se nemusejí skrýt. Zvukové formáty hudby závisí na kodecích Windows.

## Ovládání ikon plochy a omezení Windows

Windows nemá veřejné API pro skrytí samotných ikon. Aplikace vyhledá interní okno Průzkumníka `SHELLDLL_DefView\SysListView32` a přepne jeho viditelnost pomocí `ShowWindow`. Jde o nedokumentované chování Windows, které se může změnit v budoucích verzích; pokud okno nenajde nebo ovládání selže, zobrazí chybu. Po restartu Průzkumníka může být potřeba stav přepnout znovu. Ikony a soubory nejsou mazány, přesouvány ani upravovány a hlavní panel se nemění. Globální klávesová zkratka používá dokumentované `RegisterHotKey`.

## Soukromí

Aplikace neposílá žádná data na internet, nemá reklamy ani analytiku. Používá pouze místní nastavení a Windows API.

