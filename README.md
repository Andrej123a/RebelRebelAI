# Rebel Rebel

Restaurant reservation and hospitality operations management system.

**Authors:** Andrej Taleski (211120) and Simon Ilikj (211268)<br>
**Technology:** ASP.NET Core MVC, .NET 8, Entity Framework Core, PostgreSQL, ASP.NET Core Identity and SignalR

## Project Overview

Rebel Rebel connects the public guest experience with the daily operations of a hospitality venue. Guests can browse the menu and upcoming events, submit a regular or event-specific reservation, check its status with a unique code and cancel it when the current status allows it.

Staff members use a centralized administration area to review requests, assign tables through a visual floor plan, track arrivals, release occupied tables, manage events and organize weekly shifts. Each reservation has a clear status, an optional assigned table and event, an activity history and a place in the daily planner.

The goal is to replace disconnected messages, notebooks and separate tools with one consistent operational workflow.

## Main Features

### Public Website

- Branded responsive home page.
- One shared look across every guest page. It is Rebel Rebel's site first: the pub is named after a David Bowie song, so Bowie runs through it as the pub's inspiration rather than its subject. Every page starts out in deep space, drawn on one canvas as a camera would see it (`wwwroot/js/rebel-sky.js`): faint nebulae in their real colours, the Milky Way with its dark rift, distant galaxies, twinkling and falling stars, a flare on click and Ziggy's stardust on the pointer. Scrolling down turns the sky violet and then Ziggy red, rising like a sunrise (`.rs-sky-warm`), until the footer, where the sun is up; chapters melt into each other instead of meeting at hard edges. Reduced motion keeps the colours but stills the sky, and without JS the page stays in space. Bowie's characters are nine original paintings: drawn in `Rebel.Web/Art/Looks`, then painted by `Rebel.Web/Art/Paint` (lit with a modelled face and laid in stroke by stroke with bristle brushes over a warm ground) into `wwwroot/art/looks`, each with a small move of its own, and each one looks after a part of the pub (`Models/BowieCast.cs`): the home page's "How Rebel works" stage pins them and shows one per stretch of scroll with the real details and a way in (bookings, the food, the beers, the room, the hours, the way here, the next event, Rebel AI); it is a plain list without JS or with reduced motion. Each page's character stands in its hero, in front of everything, with the hero laid out so he covers none of the words. Booking a table is a talk with Major Tom: he climbs out of his tin can far above the world (a capsule drawn in `Art/Ships` and painted by `Art/Paint/ships.sh`) and asks one thing at a time (which night, what time, how many, a name, a number, and for a crew of 13 or more a word about it), you answer in the field under his question, your answers stay in the log where a tap changes them, and he reads the flight plan back before you send it up (`wwwroot/js/rebel-booking.js`). Booking for an event goes straight to him with the event's night already set, so he starts at the time. The fields are the form's own, so the server checks them as before; when it sends the form back he asks again where the problem is, and without JS every question is open at once. When the booking is sent, Ground Control radios back: his tin can keeps flying across a band of the page with the reservation code on a banner waving behind it (the only place the code is shown), and since you cannot chase a spaceship, his button below copies it for you. On the stage Life on Mars? and Ziggy hold up the menus they look after: a framed picture of the food and one of the beers, their painted fingers curled over the top of the frame (drawn in `Art/Hands`, painted by `Art/Paint/hands.sh`). They bring the picture up as they take the stage, and its photos flip over the top of the frame. Aladdin Sane, who is on the pub's mural, gets a longer turn: the room's photos hang along a wall behind him and, while you scroll, he walks you through the pub from the street to the back room, the wall sliding past as he goes. At each photo its picture light comes on and he says what it is. Without the stage the photos are a row, his words under each. The food and drinks menus are on television (Halloween Jack's channel for the drinks): a 1970s living room with a teak colour set beside the listings magazine. Each category is a channel with its own push button, the dish on air is on the set with its price in teletext, and Life on Mars? presents it from an inset in the corner, his words as subtitles. The listings are the whole menu, every dish with a still, its price and what is in it; the set follows whatever you point at and, left alone, carries on through the channel, and when nothing matches a search it shows the test card. Without JS every channel is listed. Nothing on the guest pages is a button: every action is asked of the character who looks after it (`Models/EgoCall.cs`, `Views/Shared/_Ego.cshtml`). His painted face pops out of a disc of his colour (`wwwroot/art/faces`, cut from the paintings by `Art/Paint/faces.py`), he says his line in a speech bubble, and what you say back is the action; go for him and he turns to you and talks. Major Tom (Ground Control) takes bookings, finds and cancels them and sends the booking form; Life on Mars? has the food, Ziggy the beers, Halloween Jack the bar and the night watch, the Blind Prophet Rebel AI, Pierrot the events, the Thin White Duke the way here, Aladdin Sane the room and Rebel Rebel the way home. The navbar is the cast: each part of the pub is that character's face, and hovering one shows what he does; Major Tom sits by it for bookings and Halloween Jack minds the neon OPEN sign that opens the menu, where the whole cast lines up and each one's painting comes up as you hover his line. Filters and chips are cream paper scraps; `wwwroot/js/rebel-cutup.js` gives every scrap its own cut and throws stars when you press anything (still with reduced motion; plain without JS). Like the Heroes longread it learns from, the whole site sits under film grain, headlines rise word by word as they scroll in, and each character comes into focus as he takes the stage (Aladdin Sane's bolt catches like lightning, the Duke's cigarette smokes). Three typefaces for the whole project, guest site and backstage alike: Fraunces, a soft 1970s serif, for headlines (its italic for the characters, quotes and the neon), Hanken Grotesk for reading and Space Mono for labels and anything you press (`wwwroot/css/site.css`, `wwwroot/js/rebel-site.js`). Photos keep their original files and crops; the treatments are CSS on top.
- Food menu with search, prices, descriptions, availability and dietary tags.
- Beer menu laid out as a tracklist: every beer gets its own colour-flooded section with style, ABV, origin, price, flavour notes, pairings and taste sliders built from the stored beer profile, plus style filters, search and shareable links to a single beer.
- Upcoming events with posters, details, dates and start/end times.
- Regular table reservations and reservations linked to a specific event.
- Reservation lookup and cancellation using a reservation code and phone number.
- Contact page and direct navigation to the main guest actions.

### Reservation Management

- Unique short reservation code generated for every request.
- Status workflow: `Pending`, `Approved`, `Rejected`, `Arrived`, `NoShow` and `Cancelled`.
- Search and filtering by status, date and guest information.
- Guest message and private staff notes.
- Visual table assignment based on capacity and availability.
- Manual table release after the guests leave.
- Immutable activity history for important reservation changes.
- Persistent notifications and live admin updates through SignalR.

### Floor Management

- Multiple rooms/floors with configurable dimensions and shapes.
- Tables with label, capacity, type, shape, position, size and rotation.
- Spatial fixtures such as doors, toilets, entrances, bars and terraces.
- Drag-and-drop floor editor with a visual add toolbar.
- Occupied, recommended, free and insufficient-capacity table states.
- Protection against deleting a floor that is still relevant to active reservations.

### Events

- Create, edit, publish and archive events.
- Event poster upload with file type, signature and size validation.
- Optional limits for total reservations and total guests.
- Event-specific booking list in the admin area.
- Event poster and information shown in reservation details, Tonight and Planner.
- A single event block spans its complete duration in the planner.

### Staff Scheduling

- Staff profiles for Front and Kitchen roles.
- Three predefined shifts: First, Middle and Second.
- Drag-and-drop assignment of staff to daily shifts.
- Weekly schedule and personal shift view.
- Copying the previous week without creating duplicate shifts.
- Prevention of overlapping shifts and changes to past dates.
- Manager-controlled staff accounts, roles, password reset and access state.

## User Roles

| Role | Responsibilities | Access |
|---|---|---|
| Guest | Browses the menu and events, creates and checks reservations, and cancels eligible reservations. | Public area without authentication |
| Staff | Follows reservations and the current service, handles arrivals, tables and personal shifts. | `Backstage` policy |
| Manager | Uses all operational features and manages content, configuration, staff and accounts. | `ManagerOnly` policy |
| System | Validates business rules, generates codes, records activities and protects data integrity. | Automated operations |

The legacy `Admin` role remains supported for existing accounts. Both `Admin` and `Manager` have management-level access, while `Staff` is limited to day-to-day operational functions.

## Core Workflows

### Guest Reservation

1. The guest opens **Book a Table** or starts from a specific event.
2. The guest enters contact details, date, time and party size.
3. The server validates the time rules, slot capacity and optional event limits.
4. A unique reservation code is generated and the request is stored as `Pending`.
5. An activity record and an admin notification are created.
6. The guest keeps the code for status lookup or cancellation.

### Staff Processing

1. Staff open the request from Bookings or Tonight.
2. They review the guest details, notes and optional event information.
3. They select an available table directly from the visual floor picker.
4. They approve or reject the request and the system records the decision.
5. On arrival, the reservation is marked as `Arrived`.
6. When the visit ends, the table is released and becomes available again.

### Weekly Schedule

1. A manager opens the current or next scheduling week.
2. An active staff member is selected from the staff panel.
3. The person is dragged into a First, Middle or Second shift.
4. The server validates the date and prevents overlapping assignments.
5. The previous week may be copied without duplicating existing shifts.

## Architecture

The solution follows a layered structure:

| Project | Responsibility | Examples |
|---|---|---|
| `Rebel.Domain` | Domain entities and enum values without UI dependencies. | `Reservation`, `Event`, `Product`, `PubTable`, `StaffShift` |
| `Rebel.Application` | Application contracts and a foundation for rules independent of infrastructure. | Application services and contracts |
| `Rebel.Infrastructure` | Persistence, Entity Framework Core, Identity stores and migrations. | `AppDbContext`, PostgreSQL migrations |
| `Rebel.Web` | MVC controllers, Razor Views, authorization, web services, CSS and JavaScript. | Public site, admin panel, SignalR hub |
| `Rebel.Web.Tests` | Automated tests for policies, controllers and supporting services. | xUnit tests |

A typical request flows from the browser to an MVC controller. The controller performs server-side validation and uses `AppDbContext` to execute the required operation in PostgreSQL. The result is mapped to a view model and rendered through a Razor View. When an operation creates an operational notification, the SignalR hub informs connected admin clients.

## Technology Stack

| Technology | Purpose |
|---|---|
| C# and .NET 8 | Main programming platform and runtime |
| ASP.NET Core MVC | Routing, controllers, model binding and server-rendered web application |
| Razor Views | Dynamic rendering of the public and admin interfaces |
| Entity Framework Core 8 | ORM, LINQ queries, relationships and migrations |
| PostgreSQL and Npgsql | Relational database and .NET provider |
| Neon | Managed PostgreSQL database shared by authorized development machines |
| ASP.NET Core Identity | Authentication, password management, roles and user accounts |
| SignalR | Real-time reservation notifications in the admin interface |
| HTML, CSS and JavaScript | Responsive UI, planner, drag and drop, and floor editor |
| xUnit | Automated testing |

## Data Model

### Main Entities

| Entity | Purpose |
|---|---|
| `Reservation` | Guest request, code, date, time, party size, status, table, event and notes |
| `ReservationActivity` | Chronological record of important reservation changes |
| `Event` | Event description, poster, date, time, type and capacity |
| `Category` | Product group such as Food, Drink or Beer |
| `Product` | Menu item with price, image, availability and descriptive tags |
| `FloorRoom` | Room with name, shape, position, dimensions and rotation |
| `PubTable` | Table with label, capacity, type, shape and floor coordinates |
| `FloorFixture` | Spatial element such as a door, toilet, entrance, bar or terrace |
| `StaffMember` | Operational employee profile, role, phone and active state |
| `StaffShift` | Work shift with date, start/end time, role and note |
| `Notification` | Persistent admin notification for an operational change |
| Identity entities | Users, roles, claims, tokens and authentication data |

### Important Relationships

- A `Category` has many `Product` records.
- An `Event` may have many `Reservation` records.
- A `Reservation` has many `ReservationActivity` records.
- A `FloorRoom` contains many `PubTable` and `FloorFixture` records.
- A `StaffMember` has many `StaffShift` records.
- Fixtures are deleted with their room, while tables may remain unassigned and be moved to another room.

Categories, products, events and reservations use soft deletion through `IsDeleted` and `DeletedAtUtc`. Global query filters keep archived records out of regular screens while allowing managers to restore them through the archive module.

## Business Rules

- Online reservation slots are available from 10:00 to 22:00 at 30-minute intervals.
- A new reservation must be created at least two hours in advance.
- The current policy allows a maximum of 40 online covers per time slot.
- Party size must be between 1 and 20 guests.
- Event reservations respect optional limits for reservations and guests.
- A table is recommended according to capacity and availability.
- The same table cannot be assigned to conflicting active reservations.
- Past shifts cannot be created, changed or deleted.
- A staff member cannot have overlapping shifts, including overnight shifts.
- Predefined shifts are First 10:00-16:00, Middle 12:00-20:00 and Second 16:00-00:00.
- An event without an end time is displayed until 00:00, or until 01:00 on Friday and Saturday.
- An event ending after closing time is visually limited to the closing boundary.
- Room and table labels are unique.

Database constraints complement the application checks. A filtered unique index prevents the same table from being assigned to more than one `Approved` or `Arrived` reservation for the same date and time.

## Security

- ASP.NET Core Identity provides authentication and password storage.
- Role-based authorization policies separate operational and management access.
- POST operations use anti-forgery protection and server-side validation.
- Accounts are locked for 15 minutes after five failed login attempts.
- Login, reservation creation and reservation lookup use fixed-window rate limiting by IP address.
- Production uses HTTPS, HSTS, a specific `AllowedHosts` value and a branded error page.
- Connection strings and bootstrap passwords are stored outside source control.
- The bootstrap administrator is temporary and must be disabled after the first successful setup.
- Event uploads validate extension, content type, binary signature and maximum size.

## Real-Time Notifications

SignalR is implemented in the Web layer:

- `Program.cs` registers SignalR and maps `/notificationHub`.
- `NotificationHub` requires the `Backstage` authorization policy.
- `ReservationsController` publishes reservation creation and cancellation events.
- `admin-notifications.js` receives events, displays a toast and updates the unread counter.
- Notifications are also stored in PostgreSQL, so they remain visible after a refresh.

Local SignalR connections belong to one running server process. Two developers can share the same Neon data, but live messages are not automatically exchanged between separate `localhost` instances. A shared deployment or SignalR backplane is required for multi-instance real-time delivery.

## Testing and Quality Assurance

The solution contains an xUnit test project covering web logic, policy classes, services and boundary conditions. The final verification completed successfully with **338 passing tests**, a Release build, a publish check, migration/model validation and smoke tests of critical public routes.

Key automated and manual checks include:

- Reservation code generation, allowed slots, capacity and status transitions.
- Reservation lookup and cancellation rules.
- Table recommendation, capacity, assignment conflicts and release.
- Floor layout persistence and protected deletion.
- Event image validation and storage cleanup.
- Event planner closing boundaries at 00:00 and 01:00.
- Past shift restrictions, overlap detection and weekly copy behavior.
- Manager and Staff authorization boundaries.
- Production configuration and health endpoints.

Run the complete test suite with:

```powershell
dotnet test RebelRebel.sln --configuration Release
```

## Local Setup

### Prerequisites

- .NET 8 SDK or Visual Studio with the **ASP.NET and web development** workload.
- Git.
- Access to the shared Neon project or another PostgreSQL server.

### Installation

1. Clone the repository:

   ```powershell
   git clone <repository-url>
   cd <repository-directory>
   ```

2. Open `RebelRebel.sln`.

3. In the `Rebel.Web` project, configure the database connection through User Secrets:

   ```powershell
   cd Rebel.Web
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "YOUR_NEON_CONNECTION_STRING"
   ```

4. Apply migrations in a controlled development step:

   ```powershell
   dotnet ef database update --project ../Rebel.Infrastructure --startup-project .
   ```

5. Configure the first administrator locally when needed:

   ```powershell
   dotnet user-secrets set "AdminUser:BootstrapEnabled" "true"
   dotnet user-secrets set "AdminUser:Email" "admin@example.com"
   dotnet user-secrets set "AdminUser:Password" "USE_A_STRONG_TEMPORARY_PASSWORD"
   ```

6. Start the `Rebel.Web` profile and open the HTTPS address displayed by Visual Studio.

7. After the first successful administrator login, disable bootstrap and remove the temporary password from configuration.

Never commit the Neon connection string or administrator password. Developers may use the same Neon database by storing the same authorized connection string independently in their local User Secrets. Database migrations and demo data changes must be coordinated because the data source is shared.

## Main Routes

| Module | Route | Purpose |
|---|---|---|
| Home | `/` | Public introduction and navigation |
| Menu | `/Home/Menu` | Food and Beer catalog |
| Events | `/Events` | Active event list and details |
| Reservation | `/Reservations/Create` | Create a reservation request |
| My reservation | `/Reservations/Lookup` | Status lookup and cancellation |
| Dashboard | `/Admin` | Main operational overview |
| Bookings | `/AdminReservations` | Reservation list, filters and details |
| Tonight | `/AdminReservations/Tonight` | Current-day operations |
| Planner | `/AdminReservations/Planner` | Reservation and event timeline |
| Floor | `/AdminTables` | Floor overview and editor |
| Staff | `/AdminStaffSchedule` | Staff and weekly schedule |
| Accounts | `/AdminStaffAccounts` | Accounts, roles and access |
| Events admin | `/AdminEvents` | Event CRUD, posters and bookings |
| Archive | `/AdminArchive` | Restore archived records |
| Health | `/health/live`, `/health/ready` | Application and database readiness |

## Production Readiness

Before production deployment:

- Store production secrets in the hosting provider's environment configuration.
- Set `AllowedHosts` to the real domain instead of `*`.
- Apply migrations as a controlled deployment step and verify them on staging first.
- Enable managed PostgreSQL backups.
- Verify `/health/live` and `/health/ready`.
- Verify public pages, sitemap, robots configuration and the login workflow.
- Disable bootstrap and remove the temporary administrator password.
- Run the Release build, automated tests and a smoke test of critical workflows.
- Move uploaded posters to object storage when deploying multiple application instances.

The application can be published with:

```powershell
dotnet publish Rebel.Web/04.Rebel.Web.csproj --configuration Release
```

## Current Limitations and Future Improvements

- Object storage for posters and product images instead of local `wwwroot` storage.
- More detailed audit logs for configuration changes.
- SMS or push notifications with explicit guest consent.
- Reports for occupancy, no-show rate, popular events and peak demand.
- Automatic table suggestions based on party size, time window and spatial proximity.
- POS integration and digital deposits for capacity-limited events.

Online payment, POS integration, automatic schedule optimization and a public mobile application are outside the current project scope.

## License

This repository was created as an academic software project. No separate open-source license has been assigned.
