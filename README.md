# ZEUS CONTROL v3

Panel sterowania dla Redragon Zeus Pro H510 Pro na Windows 10 x64.

## Funkcje

- regulacja poziomu domyślnego wyjścia audio przez Windows Core Audio,
- regulacja poziomu domyślnego mikrofonu,
- mute/unmute wyjścia i mikrofonu,
- płynne, rysowane suwaki z obsługą myszy i klawiatury,
- profile Gaming, Muzyka i Noc,
- zapis profilu 10-pasmowego EQ,
- wizualny model słuchawek reagujący na głośność oraz mute,
- wykrywanie nazw Zeus, H510, Redragon i XiiSound,
- diagnostyka urządzeń i czytelne komunikaty błędów,
- samodzielny plik EXE — bez VBS i PowerShella podczas uruchamiania.

## Ważne ograniczenie sprzętu

H510 Pro nie ma publicznego SDK ani aplikacji producenta. Wbudowane 7.1 oraz fizyczne RGB przełącza się przyciskami na słuchawkach. Aplikacja nie udaje, że wysłała nieistniejącą komendę USB. Kolory RGB w panelu są podglądem wizualizacji.

Profil EQ jest zapisywany w aplikacji. Zastosowanie filtrów do całego dźwięku systemowego wymaga warstwy APO/DSP, np. Equalizer APO — Windows nie udostępnia publicznego, uniwersalnego systemowego EQ.

## Budowanie

Projekt używa .NET 8 WinForms. GitHub Actions publikuje samodzielny `win-x64` i uruchamia `ZEUS_CONTROL.exe --self-test` na runnerze Windows przed udostępnieniem artefaktu.
