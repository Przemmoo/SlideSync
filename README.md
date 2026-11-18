# SlideSync

Jeden komputer (z pilotem Logitech Spotlight) steruje slajdami na pozostałych komputerach w sieci.

- **Komputer 1 (sterujący)** – przechwytuje klawisze pilota (Spotlight wysyła Page Up / Page Down) i rozsyła je po TCP.
  Lokalnie klawisze działają normalnie, więc prezentacja na komputerze 1 przełącza się jak zawsze.
- **Komputery 2, 3, … (sterowane)** – łączą się z komputerem 1 i wciskają te same klawisze w aktywnym oknie prezentacji.

Jeden plik `SlideSync.exe` (ok. 40 KB), bez instalacji – wymaga tylko Windows 10/11 (wbudowany .NET Framework 4.x).

## Użycie

1. Skopiuj `dist\SlideSync.exe` na wszystkie komputery.
2. **Komputer 1:** uruchom → rola „Komputer sterujący" → ustaw hasło (opcjonalnie) → **Start**.
   Program pokaże adres IP tego komputera. Przy pierwszym starcie Windows zapyta o zaporę – zezwól w sieci prywatnej
   (albo kliknij „Dodaj regułę zapory…").
3. **Komputery 2 i 3:** uruchom → rola „Komputer sterowany" → wpisz adres IP komputera 1 i to samo hasło → **Start**.
4. Na każdym komputerze uruchom prezentację (F5). Na komputerach 2 i 3 **kliknij w okno prezentacji**, żeby było aktywne.
5. Przełączaj slajdy pilotem na komputerze 1.

Po kliknięciu Start okno minimalizuje się (oddając fokus prezentacji); ikona w zasobniku zmienia kolor:
szary = zatrzymany, pomarańczowy = czeka/łączy, zielony = działa, czerwony = złe hasło.
Odbiornik sam wznawia połączenie po zerwaniu sieci. Opcja `SlideSync.exe /start` startuje od razu z zapisanymi ustawieniami.

## Ważne ograniczenia

- Synchronizacja jest **względna** (te same naciśnięcia klawiszy). Wszystkie prezentacje muszą zaczynać od tego samego slajdu;
  jeśli na jakimś komputerze ktoś ręcznie przestawi slajd, rozjedzie się – przywróci to klawisz Home (przekazywany domyślnie).
- Odbiornik wciska klawisze w **aktywnym oknie** – okno prezentacji musi być na wierzchu.
- Nie zadziała w oknie uruchomionym „jako administrator" (Windows blokuje to dla zwykłych programów).
- Przekazywane są pojedyncze klawisze z białej listy (nawigacja, F5, Esc, B, ., W, cyfry…). Skróty z Ctrl/Alt/Shift/Win są pomijane.
  Odbiornik ignoruje wszystko spoza listy, więc nikt w sieci nie uruchomi np. Win+R.
- Hasło idzie w sieci jawnym tekstem – to ochrona przed przypadkowym podłączeniem, nie szyfrowanie. Używaj w zaufanej sieci LAN.
- Pilot Spotlight: przycisk wskaźnika/podświetlenia działa w oprogramowaniu Logitech na komputerze 1 i nie jest przesyłany.

## Budowanie

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```

Używa kompilatora `csc.exe` z Windows (.NET Framework 4) – nie potrzeba Visual Studio ani .NET SDK. Wynik: `dist\SlideSync.exe`.
Kod źródłowy: `src\`.

## Licencja

[MIT](LICENSE) – wolno używać, kopiować, modyfikować i rozpowszechniać bez ograniczeń (także komercyjnie).

## Pobieranie

Gotowy `SlideSync.exe` jest w [Releases](https://github.com/Przemmoo/SlideSync/releases/latest).
