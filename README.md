After cloning repo rename 'appsettings-sample.json' to 'appsettings.json' and change private data in it
📦 Functionality
👤 Users
Create account / register

Login / logout

Edit personal profile

Delete account

✂️ Barbers & Services
View list of barbers

Add / edit / remove services (haircuts, beard trim, etc.)

Assign barbers to services

View service duration and price

📅 Appointments
Book appointment with selected barber and service

View upcoming and past appointments

Cancel appointment

Prevent double-booking via validation

🧾 Admin Panel
View and manage all appointments

View and manage users

Manage barber schedules

Access analytics (most popular services, busiest hours, etc.)

🧱 Architecture
The application is built using the following structure:

WPF (MVVM) – for the user interface and interaction logic

Entity Framework Core – for ORM and database access

 SQL Server – flexible for deployment and development

Repository Pattern – separation of concerns for cleaner codebase

Material Design in XAML Toolkit – for modern and clean UI

⚙️ Technologies
🧠 C# (.NET 6 / 7)

🖼 WPF (MVVM)

🗃 Entity Framework Core

💾 SQLite / SQL Server

🎨 Material Design XAML Toolkit

🧪 xUnit / MSTest (optional for unit testing)

📝 Rules and Constraints
Unique email for user accounts 📧

Appointment time must not overlap with existing ones 🕒

Only admin can manage barbers and services 🔐

🏆 Achievements
✅ Full CRUD support for users, services, and appointments

🧠 Clean MVVM architecture

🎨 Stylish and user-friendly design using Material Design

🔒 Secured authentication and role-based access

📈 Future-ready: easy to extend with reporting, statistics, or online booking
