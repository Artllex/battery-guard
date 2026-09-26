# BatteryGuard — alarm powyżej 60%

![BatteryGuard](assets/BatteryGuard.png)

Po kompilacji uruchom `dist/BatteryGuard.exe`. Program działa w tle; ikonę znajdziesz przy zegarze, czasem pod strzałką ukrytych ikon.

- Odczytuje baterię co 15 sekund i ostrzega od 61%, także gdy przy uruchomieniu bateria jest już powyżej limitu.
- Pokazuje powiadomienie Windows i odtwarza dźwięk. Gdy poziom pozostaje powyżej 60%, przypomina co 10 minut.
- Każdy odczytany wzrost powyżej 60% powoduje kolejny alarm, np. 61% → 62% → 63%. Spadek nie wywołuje alarmu, ale późniejszy wzrost ponownie go wywoła. Przy skoku np. 61% → 64% pojawi się jeden alarm z aktualnym poziomem 64%; program nie odtwarza pominiętych poziomów.
- Alarm pojawia się też przy uruchomieniu programu z baterią powyżej 60% oraz po ręcznym wybraniu **Sprawdź teraz**. Każdy alarm rozpoczyna od nowa 10 minut do kolejnego przypomnienia.
- Po spadku do 60% lub niżej ponownie uzbraja alarm.
- Prawy przycisk na ikonie → **Uruchamiaj po zalogowaniu** włącza lub wyłącza autostart dla bieżącego użytkownika. Włącz go dopiero po umieszczeniu folderu w docelowym miejscu; po przeniesieniu wyłącz i włącz tę opcję ponownie.
- **Sprawdź teraz** pozwala sprawdzić odczyt i powiadomienie. **Zakończ** zamyka monitor.

Program nie zmienia limitów G-Helper ani ASUS. Monitoruje poziom baterii również po odłączeniu zasilacza. Podczas uśpienia nie działa; po wznowieniu sprawdzi stan przy kolejnym odczycie. Widoczność powiadomień i dźwięku zależy od ustawień Windows, trybu Nie przeszkadzać i głośności.

Nie wymaga instalacji ani uprawnień administratora. Wykorzystuje Windows i .NET Framework 4.x. Aby usunąć program, najpierw wyłącz autostart, zakończ działanie i usuń folder.

Źródło: `BatteryGuard.cs`. Kompilacja w PowerShell (tworzy także ikonę ICO w wielu rozmiarach z zatwierdzonego PNG):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

Zbudowany plik `dist/BatteryGuard.exe` działa samodzielnie; ikona jest osadzona w programie. Licencja: MIT — zobacz [LICENSE](LICENSE).
