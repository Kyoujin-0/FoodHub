# FoodHub - Food Park and Food Court Centralized Ordering Hub

VB.NET (.NET 8) Windows Forms + **Microsoft Access** (.accdb). All UI is built in code (no Designer files), so just open and run.

## Setup
1. Microsoft Access (or the free "Microsoft Access Database Engine" redistributable) must be installed. Its 32/64-bit version must match the app (the app is 64-bit by default; with 32-bit Office add `<PlatformTarget>x86</PlatformTarget>` to FoodHub.vbproj).
2. Open `FoodHub.vbproj` in Visual Studio (it restores **System.Data.OleDb** and **BCrypt.Net-Next**) and press F5.
3. On the first run the app creates `database\foodhub.accdb` with all tables and relationships and fills it with sample stalls, products, banners and reviews. Open that file in Access to see the tables (Database Tools > Relationships). Close it in Access before running the app.
4. To start over: close the app, delete `database\foodhub.accdb`, run again.

`db.config` contains the path of the database file (relative to the .exe, or absolute).
Table list: `database/DATABASE_ACCESS.md`.

## Test accounts (created on first run)
| Role | Username | Password |
|---|---|---|
| Customer | customer | Customer1 |
| Admin | admin | Admin123 |
| Sellers | manang, lolaaida, brewchill, burgerbarn, sweetspot, noodlenook | Seller1 |

## Where things are
- `Core/AccessSchema.vb` creates the database and tables
- `Core/Data.vb` the `Db` helper and login / catalog / cart / orders code; other `*Svc.vb` files hold the rest of the SQL
- `Core/Seeder.vb` sample data
- `Core/Theme.vb` colors and fonts; `Core/Controls*.vb, Cards.vb, Ui.vb` custom controls
- `Forms/` windows, `Pages/` screens
