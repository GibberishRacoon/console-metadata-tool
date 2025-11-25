# console-metadata-tool

Narzędzie konsolowe do zarządzania metadanymi bazy danych Firebird 5.0 - zadanie rekrutacyjne dla Sente S.A.

## Wymagania

- .NET 8.0
- Firebird 5.0 Server
- Obsługiwane elementy: domeny, tabele (z kolumnami), procedury składowane

## Budowanie projektu
```bash
dotnet restore
dotnet build
```

## Użycie

### 1. Eksport metadanych z bazy danych
```bash
dotnet run export-scripts --connection-string "DataSource=localhost;Port=3050;Database=C:\path\to\database.fdb;User=SYSDBA;Password=masterkey;Charset=UTF8;" --output-dir "C:\output"
```

**Generuje 3 pliki:**
- `domains.sql` - definicje domen
- `tables.sql` - definicje tabel z kolumnami
- `procedures.sql` - definicje procedur składowanych

### 2. Budowanie nowej bazy ze skryptów
```bash
dotnet run build-db --db-dir "C:\newdb" --scripts-dir "C:\scripts"
```

Tworzy nową bazę `database.fdb` w podanym katalogu i wykonuje wszystkie skrypty w kolejności:
1. Domeny
2. Tabele
3. Procedury

### 3. Aktualizacja istniejącej bazy
```bash
dotnet run update-db --connection-string "DataSource=localhost;Port=3050;Database=C:\path\to\database.fdb;User=SYSDBA;Password=masterkey;Charset=UTF8;" --scripts-dir "C:\scripts"
```

Wykonuje skrypty na istniejącej bazie danych.

## Przykładowy test poprawności
```bash
# 1. Wyeksportuj metadane z testowej bazy
dotnet run export-scripts --connection-string "DataSource=localhost;Port=3050;Database=C:\temp\test.fdb;User=SYSDBA;Password=masterkey;Charset=UTF8;" --output-dir "C:\temp\exported"

# 2. Zbuduj nową bazę ze skryptów
dotnet run build-db --db-dir "C:\temp\newdb" --scripts-dir "C:\temp\exported"

# 3. Porównaj obie bazy w ISQL lub IBExpert
# Powinny być identyczne strukturalnie
```

## Ograniczenia

**Obsługiwane elementy:**
- ✅ Domeny (user-defined types)
- ✅ Tabele z kolumnami
- ✅ Procedury składowane

**Nieobsługiwane elementy:**
- ❌ Constraints (PRIMARY KEY, FOREIGN KEY, CHECK, UNIQUE)
- ❌ Triggers
- ❌ Indeksy
- ❌ Views
- ❌ Generators/Sequences
- ❌ Exceptions
- ❌ Functions (UDF)
- ❌ Roles i uprawnienia

## Technologie

- .NET 8.0
- C# 12
- FirebirdSql.Data.FirebirdClient 10.3.4
- Firebird 5.0

## Struktura projektu
```
DbMetaTool/
├── Program.cs              # Główna logika CLI
├── Helpers/
│   └── DatabaseHelpers.cs  # Metody do ekstrakcji i wykonywania skryptów
├── DbMetaTool.csproj       # Konfiguracja projektu
└── README.md
```

## Znane problemy

- Wymaga uruchomionego serwera Firebird (używa połączenia TCP/IP localhost:3050)
- Procedury muszą używać składni SET TERM dla poprawnego parsowania

## Autor
Filip A
Zadanie rekrutacyjne dla Sente S.A.
