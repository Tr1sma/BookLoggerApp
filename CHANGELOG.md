# Changelog

Alle nennenswerten Änderungen an BookHeart werden in dieser Datei festgehalten.

Format basiert auf [Keep a Changelog](https://keepachangelog.com/de/1.1.0/),
Versionierung folgt [Semantic Versioning](https://semver.org/lang/de/).

Versionsschema:

- `V0.x.y` – Pre-Release (vor Play-Store-Veröffentlichung)
- `V1.0.0` – Erster öffentlicher Play-Store-Release
- MAJOR wird auf 1 gesetzt wenn der erste public Play-Store-Upload erfolgt
- MINOR für neue Features, PATCH für Bugfixes und kleinere Änderungen

## [Unveröffentlicht]

### Behoben
- Optimistische Nebenläufigkeitssicherung (RowVersion) wirkt jetzt tatsächlich auf SQLite: Für App-Einstellungen und Abo-Status (Münzen, XP, Level, Tier) wird der Versions-Token bei jeder Speicherung neu gesetzt, sodass zwei gleichzeitige Schreibzugriffe erkannt werden, statt sich still gegenseitig zu überschreiben (verlorene Münzen/XP). Zuvor generierte SQLite den Token nie automatisch, wodurch die Konflikt-Erkennung wirkungslos und der zugehörige Schutzcode toter Code war.
- Schreibzugriffe auf die App-Einstellungen (Münzen, XP, Level) werden jetzt serialisiert, sodass z. B. eine Münz-Ausgabe beim Kauf und eine gleichzeitige XP-Gutschrift sich nicht mehr gegenseitig überschreiben können. Die Spiegelung des Abo-Status in die Einstellungen aktualisiert außerdem nur noch die Abo-Spalten und kann keine zwischenzeitlichen XP-/Münz-Änderungen mehr zurücksetzen.
- Statistik-Tabs „Trends" und „Analysen" können nicht mehr sporadisch mit leeren Diagrammen oder einem Fehler laden: Die parallel ausgeführten Statistik-Abfragen nutzen jetzt je einen eigenen Datenbank-Kontext, statt sich einen zu teilen (Entity Framework erlaubt keine gleichzeitigen Abfragen auf demselben Kontext).
- Buch speichern ist jetzt atomar: Buch-Datensatz samt Genres, Regalen, Tropes und Wishlist-Bereinigung werden in einer einzigen Transaktion gespeichert. Bricht ein Schritt ab, bleibt kein halb gespeichertes Buch (z. B. ohne Genres/Cover oder als „abgeschlossen" markiert ohne XP-/Ziel-Neuberechnung) zurück.
- Buchdetailseite wartet beim Laden jetzt — wie alle anderen Inhaltsseiten — auf die Hintergrund-Initialisierung der Datenbank, statt direkt loszulegen.
- Eingabevalidierung wird jetzt im Service-Layer durchgesetzt: Ungültige Buch-, Lesesitzungs-, Leseziel- und Pflanzendaten (leere Titel/Autoren, negative oder absurde Seiten-/Zielwerte, 0-Minuten-Sitzungen) werden vor dem Speichern abgewiesen, statt ungeprüft in die Datenbank zu gelangen. Die vorhandenen Validierungsregeln waren zuvor nie aktiv (toter Code).
- Bildschirm-Ladevorgänge lassen sich jetzt abbrechen: Beim Wegnavigieren oder schnellen Seitenwechsel wird ein noch laufender Ladevorgang abgebrochen, statt im Hintergrund weiterzulaufen und mit dem nächsten Ladevorgang um den geteilten Datenbank-Kontext zu rennen. Dafür reichen die spezifischen Repository-Methoden und die datenladenden ViewModels den `CancellationToken` jetzt durchgängig bis zur Datenbankabfrage durch.
- Abo-Status & Play-Billing-Lebenszyklus sind robuster geworden:
  - Promo-Codes (z. B. `BH-LAUNCH`/`BH-BETA2026`) werden beim Zurückholen der App in den Vordergrund nicht mehr fälschlich auf Free herabgestuft und ihre Inhalte nicht mehr ausgeblendet, solange der Code gültig ist.
  - Ein zeitlich abgelaufenes Abo wird jetzt dauerhaft auf Free gesetzt und seine Überlauf-Inhalte (zusätzliche Regale, Prestige-Pflanzen, Ultimate-Dekoration) werden ausgeblendet — zuvor wurde nur die Tarif-Anzeige im Speicher geändert, ohne Persistenz und ohne die Inhalte tatsächlich zu verbergen.
  - Aktive, sich automatisch verlängernde Abos werden nicht mehr fälschlich genau einen Abrechnungszeitraum (30 bzw. 365 Tage) nach dem Erstkauf auf Free herabgestuft; maßgeblich ist jetzt, ob Google Play das Abo noch zurückmeldet, statt eines geschätzten Ablaufdatums.
  - Google-Play-Käufe werden jetzt mit dem korrekten Kauf-Token bestätigt, sodass Play sie nicht mehr nach 3 Tagen automatisch erstattet.
  - Ein Tarif- oder Zeitraumwechsel eines bestehenden Abos läuft jetzt über den Play-Upgrade-/Proration-Flow, statt in einer „Du besitzt dieses Abo bereits"-Sackgasse zu enden.
  - Lässt sich ein gekauftes Produkt nicht zuordnen, zeigt die App jetzt einen Fehler statt einer falschen „Freigeschaltet"-Feier ohne tatsächliche Freischaltung.
- Der „Erster Monat"-Hinweis im Paywall wird nur noch angezeigt, wenn der jeweilige Tarif tatsächlich ein Einführungsangebot hat (zuvor pauschal bei beiden Monatstarifen).
- App-Neustart (z. B. nach Cloud-Wiederherstellung oder Sprachwechsel) blockiert die Oberfläche nicht mehr: Die kurze Wartezeit vor dem Beenden des Prozesses läuft jetzt ohne den UI-Thread einzufrieren, sodass kein „App reagiert nicht"-Eindruck (ANR-Risiko) auf langsameren Geräten mehr entsteht.
- JSON-Import (Backup/Merge) schlägt nicht mehr komplett fehl, wenn ein importiertes Buch dieselben internen Schlüssel wie bereits vorhandene Daten trägt (insbesondere die fest vergebenen Genre-Schlüssel): Beim Import werden Büchern und ihren Unterdaten (Lesesitzungen, Zitate, Notizen) jetzt neue Schlüssel zugewiesen und Genres anhand ihres Namens wiederverwendet statt doppelt angelegt. Zudem überspringt ein Fehler bei einem einzelnen Buch nur dieses Buch, statt den ganzen Import abzubrechen.
- Wiederherstellung aus einem Backup ist jetzt absturzsicher: Bevor die Live-Datenbank überschrieben wird, legt die App eine Sicherungskopie an. Schlägt die anschließende Schema-Aktualisierung fehl, wird die ursprüngliche Datenbank automatisch zurückgespielt, statt den Nutzer mit einer halb-aktualisierten, unbrauchbaren Datenbank zurückzulassen.
- Schema-Reparatur bei Datenbank-Aktualisierungen greift nur noch bei den tatsächlich zugehörigen „… existiert bereits"-Fehlern (Tabelle/Index/Trigger/View) und nicht mehr bei beliebigen, zufällig ähnlich lautenden Fehlermeldungen — so wird eine Aktualisierung nicht mehr fälschlich als vollständig angewendet markiert, obwohl noch Schritte fehlen.
- Lese-Abfragen (Statistiken, Suche, Listen) belasten die interne Änderungsverfolgung nicht mehr unnötig, was seltene „Instanz wird bereits verfolgt"-Konflikte vermeidet und große Statistik-Auswertungen schlanker macht.
- Lesestreak zählt jetzt nach dem lokalen Kalendertag (wie die Lesezielen), nicht mehr nach UTC: Für Nutzer außerhalb von UTC (z. B. Deutschland) bricht eine spät-abendliche oder nach Mitternacht gelesene Sitzung den Streak nicht mehr fälschlich ab bzw. zählt nicht mehr erst Stunden später als „heute".
- Zeitbasierte Statistiken (Heatmap-Tage, Wochentags- und Tageszeit-Diagramme samt „Early Bird/Night Owl"-Einordnung) sowie der Lesetrend gruppieren Sitzungen jetzt nach lokaler Zeit statt nach UTC. Abendliche Lesezeit landet damit im richtigen Tag/Bucket und am richtigen Wochentag.
- Lesesitzungs-Zeitpunkte auf der Buchdetailseite werden jetzt in lokaler Zeit angezeigt statt in UTC — zuvor konnte die angezeigte Uhrzeit (und nahe Mitternacht sogar das Datum) um den UTC-Versatz daneben liegen.
- Seiten- und Minuten-Lesezielen rechnen eine Sitzung jetzt anhand ihres Startzeitpunkts dem Zielzeitraum zu — genau wie Streak, Lesetrend und Dashboard. Eine über Mitternacht/Periodengrenze laufende Sitzung wird damit überall demselben Zeitraum zugeordnet, statt zwischen Ziel-Fortschritt und übrigen Statistiken auseinanderzulaufen.
- Das Start-Widget bestimmt aktive Lesezielen jetzt nach lokaler Mitternacht (wie die App) statt nach `DateTime.UtcNow`: Ein Ziel verschwindet am letzten Tag nicht mehr Stunden zu früh aus dem Widget und Widget und Goals-Seite zeigen denselben aktiven Stand.
- Die Verteilung der Sitzungslängen (Statistiken) zählt reine Seiten-Sitzungen ohne Lesezeit (0 Minuten) nicht mehr mit und nutzt damit dieselbe Sitzungsmenge wie die übrigen Verteilungs-Diagramme.
- Die XP-Vorschau im Lese-Timer schneidet die verstrichene Zeit jetzt wie die tatsächliche Gutschrift auf volle Minuten ab, statt aufzurunden. Zuvor zeigte der Timer während des Lesens etwas mehr XP an, als beim Speichern gutgeschrieben wurde (und nahe der 60-Minuten-Grenze fälschlich den Lang-Sitzungs-Bonus) — angezeigte und gutgeschriebene XP stimmen jetzt überein.
- Neue Bücher werden einem Regal jetzt konsistent ans Ende einsortiert — genauso wie Pflanzen und Dekorationen. Zuvor wurden Bücher vorne (Position 0) eingefügt und nur gegen andere Bücher verschoben, sodass sie auf gemischten Regalen (Bücher + Pflanzen/Dekorationen) kollidierende Positionen und eine unvorhersehbare Anzeige-Reihenfolge erzeugen konnten.
- Ein doppelter Tipp auf den Kauf-Button im Paywall startet jetzt nur noch einen Kaufvorgang. Zuvor konnten zwei schnell aufeinanderfolgende Tipps zwei Google-Play-Kaufdialoge (und doppelte Analytics-Ereignisse) auslösen.
- Hintergrund-Ereignisse (abgeschlossener Play-Kauf, Onboarding-Statuswechsel) überschreiben beim App-Start oder nach einem Kauf nicht mehr den Lade-/Fehlerzustand eines gerade laufenden Vorgangs. So flackert der Lade-Overlay nicht mehr sporadisch fälschlich aus und eine echte Start-/Onboarding-Fehlermeldung wird nicht mehr verschluckt.
- Die Sterne-Bewertung (Buch bewerten) ist jetzt per Tastatur bedienbar und für Screenreader zugänglich: Sterne lassen sich anfokussieren und mit Enter/Leertaste auswählen und werden als Bewertungselement inkl. aktuellem Wert angesagt, statt nur als „★"-Symbol vorgelesen zu werden.
- Die ISBN-Suche in der Wunschliste markiert das Suchergebnis jetzt anhand eines Erfolgs-/Fehler-Flags statt anhand des englischen Worts „found" im Meldungstext. Zuvor wurde eine erfolgreiche Suche bei deutscher App-Sprache fälschlich als Fehler (rot) dargestellt.
- Die Start-Widgets (aktuelles Buch, Lese-Streak, Tagesziel) sind jetzt vollständig lokalisiert: Alle Laufzeit-Texte (z. B. „Kein aktives Buch", „Seite x/y", „Tag-Streak", „Heute gelesen", die Einheiten Bücher/Seiten/Minuten, „Kein aktives Ziel") werden über Android-String-Ressourcen anhand der Gerätesprache aufgelöst, statt fest englisch zu sein. Zudem unterscheidet das Streak-Label jetzt tatsächlich Einzahl/Mehrzahl (zuvor toter Verzweigungscode mit identischem Text in beiden Fällen).
- Die Preis-Zusätze im Paywall (Zeitraum „Monat"/„Jahr", der „nur €2.50/Mt."-Hinweis und das „Spare 16%/30%"-Badge) werden jetzt lokalisiert statt fest englisch („month"/„year"/„just €2.50/mo"/„Save 16%") angezeigt.
- Die Banner- und Feier-Texte im Paywall (z. B. „{Tarif} freigeschaltet!", „Danke! Dein Abo ist aktiv.", „Du besitzt dieses Abo bereits.", „Kauf fehlgeschlagen. Bitte versuche es erneut.") werden jetzt durchgängig lokalisiert. Zuvor erschienen sie auch bei deutscher App-Sprache englisch.

### Sicherheit
- Abo-Features werden jetzt im Service-Layer durchgesetzt, nicht mehr nur über die Sperr-Overlays in der UI. Damit lassen sich kostenpflichtige Funktionen nicht mehr über Umwege (veraltete UI nach Tarif-Ablauf, programmatische Aufrufe) freischalten:
  - Pflanzen- und Dekorations-Käufe prüfen den jeweiligen Tarif (Plus/Premium) bevor Münzen abgebucht werden — Prestige-Pflanzen und Ultimate-Dekorationen bleiben Premium vorbehalten.
  - Genre-Filter und Buch-Ausschlüsse auf Lesezielen (Premium) werden beim Hinzufügen geprüft — der Umweg über das Bearbeiten-/Ausschluss-Modal ist geschlossen. Das Entfernen bleibt offen, damit herabgestufte Nutzer ihre Filter weiterhin bereinigen können.
  - Wishlist-Schreibzugriffe (Plus), Tropes-Verschlagwortung (Plus) und individuelle Regal-Farben (Plus) sind ebenfalls serverseitig abgesichert.
- Nach einer Herabstufung (z. B. Premium → Free) ausgeblendete Bezahl-Inhalte bleiben jetzt konsequent verborgen: versteckte Prestige-Pflanzen und die Ultimate-Dekoration erscheinen nicht mehr im Garten/Regal und fließen nicht mehr in Boost-Berechnungen ein (zuvor wurden nur Regale gefiltert).
- Eine Herabstufung von Premium auf Plus gibt Premium-exklusive Inhalte (Prestige-Pflanzen, „Heart of Stories"-Dekoration) nicht mehr frei: Plus stellt nur die ihm zustehenden Inhalte (Regale, Standard-Pflanzen/-Dekorationen) wieder her und blendet Premium-Inhalte weiterhin aus — auch wenn sie aus einer früheren Premium-Phase noch sichtbar waren.
- Datenschutz/Einwilligung (Analytics & Absturzberichte) ist jetzt durchgängig „fail-closed", also standardmäßig AUS, bis die Zustimmung bestätigt ist:
  - Firebase-Datenerfassung startet jetzt deaktiviert (Manifest-Standard auf `false`) und wird erst eingeschaltet, nachdem der Einwilligungs-Gate die gespeicherte Zustimmung als `true` bestätigt hat. Zuvor erfasste die Plattform beim Kaltstart automatisch Ereignisse (z. B. `first_open`/`session_start`), bevor die Einwilligung geprüft war.
  - Lässt sich die gespeicherte Einwilligung nicht lesen (Datenbank langsam/fehlerhaft), bleiben Analytics und Absturzberichte AUS statt sich stillschweigend einzuschalten. Auch der Fallback in der Android-Aktivität nutzt jetzt „AUS" statt „AN".
  - Nutzer-Profil-Eigenschaften (z. B. Level-/Sprache-/Onboarding-Merkmale, `environment`) werden bei deaktivierten Analytics nicht mehr an Firebase geschrieben — bisher waren nur Ereignisse, nicht aber Profil-Attribute durch die Einwilligung geschützt.
- Die App-WebView gewährt eingebettetem Web-Inhalt nicht mehr pauschal alle angeforderten Geräte-Berechtigungen: Es wird ausschließlich die Kamera (Video-Capture) freigegeben und auch nur dann, wenn die native Kamera-Berechtigung tatsächlich erteilt ist (für den Barcode-Scanner). Mikrofon und alle übrigen Ressourcen werden abgelehnt.
- Zip-Bomb-Schutz beim Wiederherstellen eines Backups begrenzt jetzt die tatsächlich entpackten Bytes statt der im Archiv deklarierten (und fälschbaren) Größenangabe. Ein manipuliertes Backup kann den Gerätespeicher so nicht mehr vollschreiben.
- Cover-Bild-Downloads sind gegen SSRF abgesichert: Es werden nur noch öffentliche `http`/`https`-Adressen geladen; `file://`, andere Schemata sowie loopback-, private und link-lokale Ziele (z. B. `127.0.0.1`, `192.168.x.x`, `169.254.169.254`) werden vor dem Abruf abgelehnt.
- CSV-Export schützt jetzt vor Formel-Injektion (CSV Injection): Felder, die mit `=`, `+`, `-`, `@`, Tab oder Zeilenumbruch beginnen, werden mit einem führenden Apostroph entschärft, damit Tabellenkalkulationen (Excel/Google Sheets/LibreOffice) sie als Text statt als ausführbare Formel interpretieren. Betrifft u. a. Titel/Beschreibung, die teils aus Fremddaten (Google-Books-API) stammen.
- Das Wiederherstellen eines Backups oder das Importieren einer JSON-Sicherung kann Bezahl-Inhalte für Free-Nutzer nicht mehr reaktivieren: Der Abo-Status wird jetzt bei jedem App-Start (nicht mehr nur bei einem Tarif-Wechsel/Ablauf) mit dem tatsächlichen Geräte-Tier abgeglichen, sodass aus einem höherstufigen Backup eingespielte Prestige-Pflanzen, Standard-/Ultimate-Dekorationen, Überlauf-Regale und Standard-Pflanzen für einen Free-Nutzer ausgeblendet (nicht gelöscht) und beim erneuten Upgrade wiederhergestellt werden. Die Ausblende-Logik erfasst dabei jetzt alle Nicht-Free-Tarife (zuvor nur Prestige-Pflanzen und Ultimate-Dekorationen). Der JSON-Import entfernt zusätzlich die Plus-Wunschlisten-Daten (`WishlistInfo`), wenn der Nutzer kein Wishlist-Recht hat.
- Aus einem wiederhergestellten höherstufigen Backup eingespielte Inhalte ohne eigenes „Ausgeblendet"-Kennzeichen werden jetzt beim Lesen anhand des aktuellen Tarifs gefiltert, statt für einen nicht berechtigten Nutzer sichtbar zu bleiben: die Wunschliste (Plus), Tropes-Verschlagwortung (Plus), Genre-/Ausschluss-Filter auf Lesezielen (Premium — der Fortschritt wird dann ungefiltert berechnet) und individuelle Regal-Farben (Plus — es werden die Standardfarben angezeigt). Die Daten werden nicht gelöscht, sondern nur ausgeblendet und erscheinen bei einem erneuten Upgrade automatisch wieder.

## [0.12.0]

### Hinzugefügt
- Stimmungen & Trigger pro Lesesitzung: Beim Beenden einer Lesesitzung (Inline- und Schnell-Timer) kannst du nach der Seiteneingabe 1–3 Emojis wählen — 😭 Tränen, 🦋 Schmetterlinge, 🌶️ Spice, 😡 Wut, 😂 Lachen, 🤯 Umgehauen. Auf der Buchdetailseite entsteht daraus eine „Emotionale Reise" — ein Verlaufsdiagramm deiner Stimmung über die Sitzungen des Buches hinweg. In den Einstellungen unter „Lese-Features" abschaltbar. Alle UI-Texte in Deutsch und Englisch lokalisiert.

## [0.11.8]

### Behoben
- Feature-Vorschlag per E-Mail (Premium): „E-Mail-Programm konnte nicht geöffnet werden" auf Android 11+ behoben — fehlende `<queries>`-Deklaration für `mailto`-Intents im Android-Manifest ergänzt und auf die native `Email.ComposeAsync`-API umgestellt.

## [0.11.7]

### Hinzugefügt
- Inline-Lese-Timer: Lesezeit vor dem Start oder in der Pause per Tippen auf die Anzeige manuell eingeben (z. B. `45:30` oder `45` Minuten). Danach erscheinen dieselben Optionen wie nach einer normalen Pause (Fortsetzen, Speichern, Seiten/XP). Gespeicherte Minuten entsprechen der angezeigten Zeit.
- Einen Predictive Burndown-Chart auf der BookDetail seite der Anzeigt wann das aktuelle Buch vorrausichtlich beendet sein wird. Auserdem eine Übersicht auf der Dashboard seite die zeigt Welche bücher vorrausichtlich als nächses beendet sein werden

## [0.11.6]

### Hinzugefügt
- Bücher per Titel suchen und automatisch ausfüllen: Neben dem bestehenden ISBN-Auto-Fill gibt es jetzt einen Such-Button direkt am Titel-Feld der Buch-Bearbeiten-Seite. Man tippt einen Titel ein (der Autor fließt mit in die Suche, falls ausgefüllt) und erhält eine Trefferliste mit Cover, Titel, Autor und Jahr. Ein Tippen auf einen Treffer füllt das Formular automatisch (inkl. ISBN, Cover, Genres). UI-Texte in Deutsch und Englisch lokalisiert.
- „Blind Date mit einem Buch" (Sub-TBR Roulette): Ein neuer Button auf dem Bücherregal öffnet eine Roulette-Ansicht, die Cover und Titel ungelesener Bücher (TBR + Wishlist) verbirgt und stattdessen nur 3–4 Vibes (Tropes, ersatzweise das Genre) auf „verpackten" Karten zeigt. Man wählt nach den Vibes und packt das Buch per Animation aus. Alle UI-Texte sind in Deutsch und Englisch lokalisiert.
- Live-Lese-Timer als dauerhafte Benachrichtigung (Android-Vordergrunddienst): Während einer aktiven Lesesitzung erscheint auf dem Sperrbildschirm und in der Statusleiste eine Benachrichtigung mit Buchtitel und mitlaufender Zeit. Über die Schaltflächen lässt sich der Timer pausieren/fortsetzen oder stoppen; „Stopp" pausiert und öffnet die Buchdetailseite (mit dem dort pausierten Lese-Timer), damit die Seite bestätigt und normal gespeichert werden kann. Ein Tippen auf die Benachrichtigung öffnet ebenfalls die Buchdetailseite. Die Benachrichtigungstexte folgen der in der App gewählten Sprache (Deutsch/Englisch). Lässt sich in den Einstellungen unter „Benachrichtigungen → Live-Lese-Timer" abschalten.

### Behoben
- Feature-Vorschlag per E-Mail (Premium): „E-Mail-Programm konnte nicht geöffnet werden" auf Android 11+ behoben — fehlende `<queries>`-Deklaration für `mailto`-Intents im Android-Manifest ergänzt und auf die native `Email.ComposeAsync`-API umgestellt.

## [0.11.5]

### Behoben
- Ziel-Löschen aus dem Bearbeitungs-Modal: Bestätigungsdialog wurde hinter dem Modal verdeckt (gleicher z-index), Zielname fehlte im Dialog, und das Ziel wurde nach Bestätigung nicht gelöscht. Bestätigungsdialog hat nun z-index 2000 und speichert ID/Name vor Modal-Reset.
- Die Onboarding-Mission für die erste geloggte Lesezeit verwendet in der deutschen UI jetzt konsistent den Begriff „Lesesitzung“ statt „Lesesession“.

## [0.11.4]

### Behoben
- „Als Nächstes"-Missionstext im Erste-Schritte-Banner jetzt lokalisiert – statt dem englischen Rohtext erscheint der übersetzte Missionstitel.
- Navbar-Eintrag für Dashboard zeigte auf Deutsch „Übersicht" statt dem korrekten englischen Begriff „Dashboard".

## [0.11.3]

### Geändert
- Startgeschwindigkeit verbessert: die veraltete Legacy-Datenbank-Migration (Pfad `Personal` → `LocalApplicationData`) wurde vom Startpfad entfernt. Sie lief bei jedem Kaltstart synchron und durchsuchte das Dateisystem, obwohl alle Bestandsdaten längst am aktuellen Speicherort liegen. Bestehende Daten sind nicht betroffen.

### Behoben
- Wunschlisten-Prioritäten werden in der Bücherregal-/Wunschlistenansicht jetzt korrekt in der aktiven App-Sprache angezeigt statt als englische Enum-Werte.
- Bücher/Wishlist-Einträge lassen sich über die Schnell-Hinzufügen- und Wishlist-Modal-Flows nicht mehr ohne Autor anlegen; der Pflichtfeld-Hinweis wird jetzt in beiden Wegen korrekt erzwungen.

## [0.11.2]

### Hinzugefügt
- Alle Missions- und Feature-Atlas-Texte auf der "Einstieg"-Seite vollständig lokalisiert (Titel, Beschreibungen, CTA-Labels, Badges, Notizen).

### Geändert
- Bewertungskategorien werden in Buchdetails und Statistiken jetzt über die aktive App-Sprache lokalisiert.

### Behoben
- Onboarding-Mission "Buch vollständig bewerten" wurde nach dem Umstieg auf genre-abhängige Bewertungskategorien nicht mehr abgeschlossen, weil sie weiterhin die alten 6 festen Felder (inkl. Spice Level) prüfte. Sie gilt jetzt als erfüllt, sobald ein abgeschlossenes Buch in allen für sein Genre angezeigten Kategorien bewertet ist.

## [0.11.1]

### Hinzugefügt
- Deutsche Sprachunterstützung inkl. Sprachauswahl in den Einstellungen (Englisch + Deutsch). Beim allerersten Start wird die System-Sprache automatisch erkannt; ein späterer Sprachwechsel startet die App neu, damit die neue Sprache überall greift.
- Neue Sektion "🌐 Sprache" in den Einstellungen mit Dropdown, das zwischen allen unterstützten Sprachen umschalten lässt.
- Komplette UI übersetzt: Navigation, alle Pages (Regal, Dashboard, Statistik, Ziele, Lesen, Buchdetails, Buch bearbeiten, Shop, Getting Started, Einstellungen), alle Shared-Components (Karten, Modals, Celebrations, Widgets, Paywall, Onboarding), ViewModel-Fehlermeldungen, FluentValidation-Meldungen und Android-Widget-Beschreibungen.
- Android-Widget-Strings jetzt zweisprachig (`values/strings.xml` EN, `values-de/strings.xml` DE) — das System wählt automatisch die richtige Datei basierend auf der Geräte-Sprache.
- Notifications (Lese-Erinnerung, Ziel-Abschluss, Pflanzen-Wasserbedarf) werden in der aktiven UI-Sprache angezeigt.

### Geändert
- `AppSettings.Language` wird beim ersten App-Start automatisch aus der System-Sprache abgeleitet (statt immer `en`).
- Backup-Restore synchronisiert die Sprach-Preference nach einem Restore mit dem wiederhergestellten `AppSettings.Language`, damit die App nach Neustart in der korrekten Sprache läuft.
- `SchemaDriftGuard` repariert jetzt nicht nur `AppSettings`-Spalten, sondern auch alle Spalten und die `UserEntitlements`-Tabelle aus der `AddPremiumSubscriptionSystem`-Migration. Drift-Reparaturen werden weiterhin als Crashlytics-Non-Fatals mit `table`/`column`-Keys gemeldet.

### Behoben
- SQLite-Fehler "no such column: u.IsHiddenByEntitlement" auf Geräten, deren `AddPremiumSubscriptionSystem`-Migration nach dem ersten `ALTER TABLE` durch `MigrationRecovery.IsSchemaAlreadyAppliedError` als applied markiert wurde, bevor die übrigen 8 ALTER TABLEs / die `UserEntitlements`-Tabelle / die Seed-Rows liefen. `SchemaDriftGuard` legt fehlende Spalten und die Tabelle jetzt beim nächsten Start nach.
- Books-Seite zeigte nach einem Lade-Fehler einen leeren Listen-View statt einer Fehlermeldung — jetzt erscheint der gleiche `alert-danger`-Block mit Retry-Button wie auf dem Dashboard.

## [0.10.6]

### Hinzugefügt
- Celebration-Modal mit Konfetti nach erfolgreichem Einlösen eines Promo-Codes ("Successfully redeemed code") und nach Abschluss einer Subscription in der Paywall.
- Telemetrie für Datenbank-Initialisierung (Gesamt- und Teilschritt-Dauern) — hilft bei der Analyse von langsamen Geräten
- DB-Init-Log in "Data Recovery Diagnostics" (Einstellungen) sichtbar — zeigt pro Schritt (CanConnect, MigrateAsync, SchemaDriftGuard, Deferred-Steps) Dauer und Fehler. Wird beim Aufklappen automatisch aktualisiert, damit Nutzer den Log zum Support weitergeben können

### Geändert
- Datenbank-Initialisierung läuft jetzt auf einem dedizierten Hintergrund-Thread statt über den ThreadPool. Auf bestimmten Geräten (u.a. Samsung Galaxy A16) wurde der Start dadurch nicht mehr rechtzeitig fertig.
- Timeout für DB-Initialisierung von 45 s auf 20 s reduziert — schnellerer Zugriff auf den Retry-Button, wenn wirklich etwas hängt
- Retry-Button für die Datenbank-Initialisierung startet jetzt immer einen frischen Versuch (vorher nur, wenn das Failure-Flag gesetzt war — bei reinen Timeouts blieb die App dadurch hängen)
- Retry ist idempotent: mehrfaches Tippen spawnt keine parallelen Initialisierungs-Tasks mehr

### Behoben
 - Goals können jetzt nicht mehr zu mehr als 100% abegschlossen werden
 - "Loading..."-Zustand auf allen Seiten löste sich nach Property-Änderungen im ViewModel nicht automatisch auf — Blazor-Komponenten beobachten jetzt `INotifyPropertyChanged` über eine zentrale Base-Klasse
 - Retry-Button nach DB-Timeout hing in einer Schleife, weil der Helper-Zustand nicht zum UI-Zustand passte (TimeoutException im ViewModel markiert jetzt auch den Helper als gescheitert)

## [0.10.5]

### Hinzugefügt
- Beim ersten Start nach dem Onboarding erscheint ein dezenter, nicht-blockierender Datenschutz-Banner
- Datenschutzerklärung (DE + EN) um einen ausführlichen Firebase-Abschnitt erweitert
- Changelogs vollständig überarbeitet/verbessert
- Beim Deaktivieren der Nutzungsstatistiken wird die anonyme Geräte-ID zurückgesetzt (`ResetAnalyticsData`), sodass künftige Ereignisse — falls wieder aktiviert — als neuer anonymer Nutzer erscheinen


### Geändert

### Behoben

## [0.10.4]

### Hinzugefügt
- Promo-Code-Eingabefeld in der Paywall mit Prefix `BH-` für interne Codes (z. B. `BH-BETA2026` für 30 Tage Plus). Hochwertige Einmal-Belohnungen wie Lifetime Premium laufen über Google-Play-native Promo-Codes, die im Play Store eingelöst werden.
- Firebase Analytics und Crashlytics für Android integriert — anonyme Nutzungsstatistiken und Absturzberichte helfen, die App zu verbessern (Buchtitel, Autoren, Notizen, Zitate und andere persönliche Daten werden **nicht** übertragen)
- Neuer Bereich „🔒 Datenschutz" in den Einstellungen: separate Toggles für Nutzungsstatistiken und Absturzberichte, jederzeit deaktivierbar

### Geändert

### Behoben


---
## [0.10.3]

### Hinzugefügt
- Premium-Subscription-System mit zwei Tiers: **Plus** (2,99 €/Monat · 29,99 €/Jahr) und **Premium** (11,99 €/Monat · 99,99 €/Jahr · 99,99 € Lifetime als Launch-Special, danach 249,99 €). Free-Stufe bleibt vollständig nutzbar: unbegrenzt Bücher, Lese-Timer, Basis-Statistiken, XP/Coins, 4 Starter-Pflanzen, 3 Starter-Dekorationen, alle Widgets und komplettes Backup/Export/Import. Plus schaltet unbegrenzte Regale, Notizen & Zitate, Wishlist, Tropes und den vollen Shop frei. Premium ergänzt die Trends- und Insights-Statistik-Tabs, Share-Cards, Prestige-Pflanzen, das Herz der Geschichten, gefilterte Reading-Goals und Google Play Family Sharing.
- Neue Paywall-Modal mit Feature-Vergleichstabelle, kontextuellem Titel beim Tippen auf ein gesperrtes Feature und Preisbuttons für Monat/Jahr/Lifetime — aufrufbar über jede `LockedFeatureButton`-Hülle oder manuell via Settings.

### Geändert

### Behoben
- Release-Builds crashten direkt beim Start mit `IllegalStateException: The Crashlytics build ID is missing` aus `FirebaseInitProvider.onCreate`. Ursache: die Firebase-Crashlytics-Gradle-Plugin-Integration existiert in .NET MAUI nicht, entsprechend wird die erwartete String-Resource `com.crashlytics.android.build_id` nie generiert und Crashlytics wirft beim Auto-Init. Fix: Platzhalter-Build-ID in `Platforms/Android/Resources/values/crashlytics-build-id.xml` bereitgestellt (GUID per Release manuell neu generieren).

## [0.9.6]

### Behoben

- Onboarding- und Update-Overlays waren auf Geräten mit klassischer 3-Tasten-Navigation teilweise verdeckt
- Android-Widget zeigt den Ziel-Fortschritt jetzt korrekt in der lokalen Zeitzone (zuvor konnten Bücher bei UTC-Offsets in der falschen Periode landen)
- Gieß-Benachrichtigungen berücksichtigen jetzt den Herz-der-Geschichten-Wachstumsbonus — Hinweise feuern rechtzeitig vor dem Durstig-/Welk-Zustand
- Buch-Fortschritt wird auf 0–100 % begrenzt; die aktuelle Seite wird nicht mehr über die Seitenzahl hinaus gespeichert (zuvor zeigte die UI z.B. „600 / 500 (100 %)")
- Mehrfaches Tippen auf „Abschließen" vergibt nicht mehr doppelt XP und überschreibt das Abschluss-Datum nicht
- Plant-Widget berechnet mit aktivem Herz der Geschichten die „Tage bis Level-Up" jetzt korrekt
- Lese-Ziele nutzen die lokale Zeitzone statt UTC — Bücher am späten Abend werden korrekt dem laufenden Tag zugeordnet
- Pflanzen- und Dekorations-Käufe erstatten Münzen zurück, wenn das Speichern fehlschlägt; fehlgeschlagene Käufe verteuern den nächsten Kauf nicht mehr
- „Alle Daten löschen" entfernt jetzt auch gekaufte Dekorationen und ihre Regal-Platzierungen
- Level-Up-Celebration erscheint auch, wenn nur der „Herz der Geschichten"-First-of-Day-Bonus über eine Level-Grenze hebt
- Beim gleichzeitigen Ändern von Buch-Status und Genre wird das Buch korrekt dem neuen Genre-Ziel zugeordnet
- XP-Berechnung mit Pflanzen-Boost rundet mathematisch korrekt (vereinzelte 1-XP-Verluste pro Session behoben)
- Doppel-Tap auf „Awesome!" in Session-Celebrations zeigt keine überlappenden Overlays mehr
- Abgebrochene Backup-Imports räumen halb kopierte Zwischendateien im Cache auf
- Shop-Sortierung (Pflanzen & Dekorationen) bleibt nach Reload konsistent
- Buch-Import mit zukünftigem Abschluss-Datum wird jetzt zuverlässig abgewiesen

## [0.9.4]

### Hinzugefügt

- Neue Prestige-Pflanze **Chronikbaum** (Lv 45 · 20.000 🪙 · 30 % XP-Boost) mit Streak-Wächter: rettet alle 14 Tage automatisch einen brechenden Lese-Streak
- Neue Prestige-Pflanze **Ewiger Phönix-Bonsai** (Lv 57 · 80.000 🪙 · 50 % XP-Boost) mit Phönix-Schutz: wiederbelebt sich selbst und schützt alle anderen Pflanzen vor dem Sterben
- Neue Ultimate-Dekoration **Herz der Geschichten** (Lv 70 · 200.000 🪙, nur 1× kaufbar): +25 % globaler XP-Boost, +25 % auf Level-Up-Münzen, +400 🪙 ab 30-Minuten-Sessions, doppeltes Pflanzenwachstum und +2,5 % der Next-Level-XP auf die erste Lese-Session des Tages
- Legendary-Visuals im Shop: warmer Beige-Rand, sanftes Pulsieren und „✨ Legendär"-Badge für Items mit Spezialfähigkeit
- Shop-Detail-Ansicht zeigt vor dem Kauf eine Beschreibung der Spezialfähigkeit

### Geändert

- Session-Abschluss-Celebration hebt Streak-Rettung und Herz-der-Geschichten-Boni jetzt explizit hervor

### Behoben

- Plant-Boost-Berechnung nutzt einen gemeinsamen Helper — UI-Anzeige und XP-Gewährung können nicht mehr voneinander abweichen
- Pflanzen-Level-Up-Münzen werden erst nach erfolgreichem Speichern gutgeschrieben (bei DB-Konflikten bleiben Münzen und Level konsistent)
- Dashboard-Wochenstatistiken („Diese Woche") beginnen jetzt am **Montag** (zuvor Sonntag, wodurch der Vorsonntag fälschlich mitgezählt wurde)

## [0.9.3]

### Geändert

- Coin-Belohnung pro Level-Up wächst jetzt progressiv statt linear (Formel: 50×Level + 3×Level²)
- Bessere Pflanzen geben einen deutlich höheren XP-Boost
- Mehrere kleine UI/UX-Updates und Verbesserungen

## [0.9.2]

### Hinzugefügt

- Erweiterte Statistiken mit 3-Tab-System (Übersicht | Trends | Analysen) auf der Statistik-Seite
  - Trends: Lese-Kalender (Heatmap), Wochentag- und Tageszeit-Analyse mit Fun-Labels (z.B. „Nachteule 🦉"), Session-Längen, Monats-Leseverlauf, Seiten/Stunde, durchschnittliche Lesedauer pro Buch
  - Analysen: Jahresvergleich, Genre-Radar, Abschlussquote, Buchlängen-Vorliebe, Top-Autoren
- Genre-spezifische Bewertungskategorien: 5 neue Kategorien (Spannung, Humor, Informationsgehalt, Emotionale Tiefe, Atmosphäre) ergänzen die bestehenden 6. Beim Bewerten werden nur passende Kategorien angezeigt, weitere per Dropdown aufklappbar.

### Geändert

- XP-Boosts für höherstufige Pflanzen deutlich erhöht (z.B. Mystic Tome Tree: 20% → 75%, Ancient Bonsai: 15% → 50%)
- Shop-Seite (Pflanzen & Dekorationen) deutlich kompakter: dichteres Kartenraster (zwei Spalten statt einer auf kleinen Handys), kompakteres Kaufen-Modal
- Settings-Seite aufgeräumt: Hero-Karte mit Version, thematische Abschnitte („Data & Backup", „Help & Community", „More Info"), reduzierte Abstände
- Stats-Seite: „Top Rated Books" kompakter (Inline-Rang, scrollbare Filter, 3 statt 5 Einträge); „Level Milestones" als Mini-Karten-Grid mit bis zu 7 Spalten
- „Getting Started"-Seite auf kleinen Displays kompakter

### Behoben

- Changelog wird nach einem Update wieder zuverlässig angezeigt, auch wenn die aktuelle Version keinen eigenen Eintrag hat

## [0.9.0]

### Hinzugefügt

- Breite Regalgegenstände: Dekorationen mit mehreren Slots (z.B. Globus, Marmorbuchstütze, Teleskop, alte Schriftrolle = 2 Slots)
- Neue Regaldekorationen im Shop (Kerzen, Stundenglas, Eulen-Figur, Globus u.v.m.) — rein kosmetisch, günstig, per Level freigeschaltet
- Shop-Seite hat jetzt Tabs für Pflanzen und Dekorationen
- Versioniertes Onboarding mit Intro-Overlay und neuer „Getting Started"-Hub für geführte Missionen (erstes Buch, erste Lesesession, Ziele, Pflanzen, Wunschliste, Scanner, Sharing, Backup)
- „Getting Started"-CTA auf Bücherregal und Dashboard sowie Einstieg in den Settings zum erneuten Öffnen des Intros

### Geändert

- 11 von 14 Dekorationen sind jetzt 2 Slots breit für bessere Erkennbarkeit im Regal (u.a. Stundenglas, Tintenfass & Feder, Magische Leselampe)
- Onboarding-Overlay kompakter und farblich einheitlich dunkler an das BookHeart-Theme angepasst

### Behoben

- Tote Pflanzen werden beim Löschen vollständig entfernt (inklusive Regal-Verknüpfungen) und tauchen nicht mehr als „Add Plant"-Option auf
- Dashboard stürzt nach einem Cloud-Backup-Restore nicht mehr ab, wenn eine Pflanzenspezie-Verknüpfung fehlt
- Stats-Seite: Division durch null bei der Pflanzenverstärkungs-Anzeige behoben (wenn Pflanzen vorhanden, aber XP-Boost = 0)
- Korrupte Cloud-Backups werden vor der Wiederherstellung per SQLite-Integritätsprüfung abgelehnt statt die aktive Datenbank stillschweigend zu überschreiben
- Automatischer Neustart nach Cloud-Backup-Wiederherstellung funktioniert jetzt zuverlässig auf Android 12+
- Cloud-Backup-Restore erzeugt keinen „database disk image malformed"-Fehler mehr: die App startet nach erfolgreichem Restore automatisch neu, damit SQLite-Verbindungen, File-Handles und Blazor-Komponenten frisch aufgebaut werden
- Erstinstallations-Race behoben: der erste Cloud-Backup-Restore auf einer frischen Installation konnte die Datenbank zuvor korrumpieren, weil der Startup-Initializer noch eine aktive Verbindung hielt

## [0.8.2] - 2026-04-07

### Hinzugefügt

- Beim Beenden einer Lesesession erscheint bei aktiver Lese-Streak eine eigene Streak-Feier mit zusätzlichem, nach Streak-Tagen skaliertem XP-Bonus

### Behoben

- Stats-Seite stürzt nicht mehr ab, wenn vorhandene, aber tote Pflanzen keinen aktiven XP-Boost mehr beitragen
- Globale Fehleransicht wird nach einem Seitenwechsel wieder korrekt zurückgesetzt, statt weitere Seiten fälschlich als abgestürzt anzuzeigen

## [0.8.1] - 2026-04-07

### Hinzugefügt

- Pflanzen im Bücherregal können direkt im Detail-Modal über ein Stift-Icon umbenannt werden
- BookHeart prüft auf Android auf verfügbare Play-Store-Updates und zeigt nach einem App-Update die passenden Changelog-Einträge an

### Geändert

- README-Featureübersicht vollständig an die seit V0.1.0 hinzugefügten Funktionen angepasst
- Pflanzen-Detail-Modal im Bücherregal insgesamt kompakter gestaltet

### Behoben

- Projektweiter Razor-/Restore-Fehler auf Windows behoben
- Android-App-Icon verwendet für Launcher-Themes keine monochrome Variante mehr — Icon Themes überlagern BookHeart nicht mehr einfarbig
- Backup-Restore findet `booklogger.db` und den `covers`-Ordner jetzt auch bei abweichender Groß-/Kleinschreibung in ZIP-Inhalten
- Start eines Buches ist jetzt idempotent: `DateStarted` bleibt beim erneuten Start erhalten und der Status wird nicht von „Reading"/"Completed" überschrieben
- Reading-Timer aktualisiert die Laufzeit threadsicher über den UI-Dispatcher
- Scanner-Abschlusslogik beim Schließen der Scanner-Seite robuster gemacht
- Seitenvalidierung beim Beenden von Lesesessions berücksichtigt jetzt die Startseite der Session
- Buchabschluss beim Speichern bewertet den tatsächlichen Datenbankstatus und verhindert doppelte XP-Vergabe
- Beim Verlassen der Wishlist beim Speichern wird die Bereinigung anhand des tatsächlich persistierten Status entschieden
- Beim Bearbeiten von Wishlist-Büchern bleibt `WishlistInfo` erhalten, solange der Status nicht geändert wird
- Tippen auf Pflanzen im Bücherregal öffnet jetzt zuverlässig das Pflanzen-Modal; das Entfernen aus dem Regal wurde in das Detail-Modal verlegt

## [0.8.0] - 2026-04-07

### Geändert

- Pflanzen im Bücherregal öffnen ein Detail-Modal mit Namen, Level, nächstem Gießzeitpunkt und Gießbutton
- Pflanzennamen im Shop-Kaufdialog und im Pflanzen-Modal etwas größer dargestellt

### Behoben

- Plant-Shop-Karten zeigen bei nicht bezahlbaren Pflanzen nur noch den Preis, nicht mehr den Zusatz „Need X more"
- Pflanzenstatus und Gießlogik werden beim Laden und Interagieren zuverlässiger aktualisiert
- Drag-and-Drop-Einfügebalken im Bücherregal richtet sich jetzt an der Höhe des Ziel-Buchs aus statt über die gesamte Regalhöhe zu laufen

## [0.7.6] - 2026-04-02

### Hinzugefügt

- Cover-Bild aus Galerie wählen: Beim Bearbeiten oder Anlegen eines Buches kann das Cover direkt aus der Geräte-Galerie ausgewählt werden (Android). Das Bild wird sofort lokal gespeichert, sodass es auch bei einmaliger Berechtigungsvergabe erhalten bleibt.
- Android-Home-Screen-Widgets: „Aktuelles Buch" (Cover, Titel, Fortschritt), „Lese-Streak" (Streak-Tage mit Heute-Status) und „Lese-Ziel" (Fortschritt zum aktiven Leseziel). Automatische Aktualisierung alle 30 Minuten und sofort nach Änderungen in der App.
- Regalfarbe anpassbar: In den Settings können Bücherleisten und Regalleiste getrennt aus je 8 Holzfarb-Presets gewählt werden
- Android-Zurück-Button in die App eingebunden

### Behoben

- Buttons (z.B. „Add Shelf", „Backup to Cloud") blieben nach dem Tippen visuell hervorgehoben — Hover-Styles werden jetzt nur noch auf Geräten mit Maus/Trackpad angewendet

## [V0.7.5] - 2026-04-02

### Hinzugefügt

- Stats teilen (Reading Wrapped): Auf der Stats-Seite können Lesestats als PNG im Instagram-Story-Format (1080×1920) geteilt werden. Zeitraum wählbar: Week, Month, Quarter, Year, All Time. Die Karte zeigt abgeschlossene Bücher, gelesene Seiten, Lesezeit, Lieblingsgenre und Top-3-Bücher.
- Buchempfehlung teilen: Nach dem Abschließen eines Buchs erscheint in der Completion-Feier ein „Share as Recommendation"-Button. Die Share-Karte zeigt Titel, Autor, Cover, Seitenanzahl, Lesezeit, Gesamtbewertung mit Sternen und alle Einzelbewertungen mit farbigen Fortschrittsbalken.
- Share-Icon auf der Buchdetailseite neben „Book Information" für alle abgeschlossenen Bücher
- „HIGHLY RECOMMENDED"-Badge auf der Share-Karte ab einer Bewertung von 4.0

### Geändert

- Vereinfachter Review-Flow: zwei einfache Modals statt des bisherigen komplexen Flows — erst App-Feedback, dann optional der Sprung zur Play-Store-Bewertung
- Review-Dialog erscheint erst ab Level 7, höchstens zweimal pro Monat und kann über „Nicht mehr fragen" dauerhaft abgeschaltet werden
- Bei Buchabschluss wird direkt die Book-Completion-Feier angezeigt (ohne vorheriges Session-XP-Modal)

## [V0.7.4] - 2026-04-01

### Geändert

- „Wie Leveln Pflanzen?"-Absatz auf der Plant-Shop-Seite auf Englisch übersetzt
- Backup-Dateiname von `booklogger_backup_*.zip` auf `bookheart_backup_*.zip` umbenannt

### Behoben

- Untere Android-NavBar überdeckt die In-App-NavBar nicht mehr
- ISBN-Autofill nutzt automatisch einen anonymen Fallback ohne API-Key, wenn Google Books für den Projekt-Key eine Quota-/Rate-Limit-Fehlermeldung liefert

## [V0.7.3] - 2026-03-31

### Hinzugefügt

- Changelog-Datei
- Google Play In-App-Review-Integration: Review-Dialog nach Level-Up, Buch-Abschluss oder Leseziel-Erreichen (max. 2×/Monat, erst ab Level 6)

### Geändert

- Buy-me-a-Coffee-Unterstützung direkt im Backup-&-Restore-Bereich der Einstellungen
- Layout der Settings-Seite optimiert
- App-Symbol auf neues Design umgestellt

## [V0.6.3] - 2026-03-30

### Hinzugefügt

- Bodenfläche unter jedem Buch im Bücherregal (Issue #159)
- Bodenfläche für Pflanzen

### Geändert

- Benachrichtigungen verbessert

### Behoben

- Kamera- und Benachrichtigungs-Berechtigungsfehler behoben (PR #158)
- Zielfortschritts-Bug behoben

## [V0.6.2] - 2026-02-18

### Geändert

- Sicherheits- und Abhängigkeits-Updates (Dependabot)

## [V0.6.1] - 2026-02-18

Internes Wartungs-Release.

## [V0.6.0] - 2026-02-18

### Hinzugefügt

- Drag-and-Drop für Bücherregale (mit Long-Press-Geste und Auto-Scroll)
- Push-Benachrichtigungen
- Automatische Bildskalierung für große Buchcover
- Bücher können aus Zielen ausgeschlossen werden
- Ziele können auf bestimmte Genres oder Tropes beschränkt werden
- Timer-Hintergrundstatus konsistent über alle Timer-Komponenten

### Geändert

- App-Name zu **BookHeart** geändert
- Regal löschen verschiebt Bücher automatisch ins Hauptregal
- Scroll- und Drag-and-Drop-Performance verbessert

### Behoben

- ISBN- und Language-MaxLength-Fehler im Datenbankschema
- Division durch Null in StatsService

## [V0.5.4] - 2026-02-13

### Hinzugefügt

- Mehrkategorien-Bewertungssystem mit Datenbankmigrierung
- StatsViewModel mit umfassenden Lesestatistiken
- Nutzerfortschritts-Tracking und Einstellungen mit Import/Export

### Geändert

- Buchsuche verbessert (Pflanzenbelohnungen, Regal-Sortierung)
- Backup-Wiederherstellung zuverlässiger (SQLite WAL-Dateien, virtuelle Pfade für Google Drive)

## [V0.5.1] - 2026-01-14

### Hinzugefügt

- Dependabot-Konfiguration für automatische NuGet-Sicherheitsupdates
- Automatische Sortierung für spezielle Regale

### Sicherheit

- **[HOCH]** Zip-Bomb-Schwachstelle in ImportExportService behoben

## [V0.5.0] - 2026-01-13

### Hinzugefügt

- Tropes/Subgenre-Tagging für Bücher
- Lazy Loading für Buchcover (IntersectionObserver)
- Cloud-Backup-Funktion
- Debounced-Suche mit Genre- und Trope-Unterstützung

### Geändert

- Gamification rebalanciert (neue Pflanzen, angepasste XP-Kurve)
- StatsService-Datenbankabfragen optimiert (DB-seitige Aggregation)

### Sicherheit

- **[HOCH]** Zip-Slip-Schwachstelle in ImportExportService behoben
- URL-Parameter-Injection in LookupService behoben
- Sicherheitsaudit für Image-Download

## [V0.4.0] - 2026-01-06

### Hinzugefügt

- Natives MAUI-Barcode-Scanning (ZXing.Net.Maui.Controls)
- Benutzerdefinierter Farbwähler für Buchrücken (erweitertes Farbspektrum)
- Android-Zurück-Button-Support
- AOT-Kompilierung im Release-Modus (bessere Performance)
- Plattformspezifische async Einstellungen und Datei-Saver

## [V0.3.0] - 2025-12-04

### Hinzugefügt

- Google-Play-Store-Vorbereitungen
- Ziel-Events und Datenlöschungsfunktion

### Geändert

- Pflanzen-Leveling auf Lesetage umgestellt
- Mobile UX und Berechtigungsverwaltung verbessert

### Behoben

- Race Condition bei Münz-Updates

## [V0.2.0] - 2025-11-07

### Geändert

- Migration auf .NET 10

## [V0.1.0] - 2025-11-04

### Hinzugefügt

- Initiale Veröffentlichung
- Bücher verwalten (hinzufügen, bearbeiten, löschen, Buchcover)
- Leseziel-Tracking
- Gamification: XP, Level, Pflanzen, Shop, Coins, Streaks
- SQLite-Datenbank mit EF Core
- Android-App (MAUI Blazor Hybrid)
