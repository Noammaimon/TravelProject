# Travel Agency Management System (MVC)

* **Name:** Noam Maimon
* **ID:** 212994297
* https://github.com/Noammaimon/TravelProject.git
## Project Overview
This web application is a professional Travel Agency Service platform developed as part of the "Introduction to Computer Communications" course ,The system is built using the ASP.NET Core MVC framework and implements a complete end-to-end solution for trip discovery, booking management, and administrative oversight.

The project emphasizes strict adherence to business logic, automated background processes, and a secure relational database architecture.

## Functional Requirements Implementation

### Customer Interface
-**Search and Filtration:** Advanced engine for locating trips based on destination, country, and trip category.
- **Booking Constraints:**
    - **Active Booking Limit:** Logic enforced to prevent users from exceeding 3 simultaneous active bookings.
    - **Waiting List Management:** Automated queue system for fully booked trips.
    - The system monitors availability and promotes users from the waiting list when spots become available.
    - **Itinerary Generation:** Capability for users to download trip itineraries in document format following successful confirmation.
- **Transaction Flow:**
    - **Shopping Cart:** Support for managing multiple selections or immediate "Buy Now" processing.
    - **Automated Notifications:** Mandatory email registration to facilitate automated booking, payment confirmations, and trip reminders.
-**Feedback System:** Users can rate and review both specific trips and the overall service experience.

### Administrative Interface
- **User Management:** Full administrative control over user accounts, permissions, and booking history.
- **Trip Administration:** Adding/removing travel packages and managing specific trip instances.
- **Dynamic Pricing:** Ability to adjust prices and apply temporary discounts with visual indicators for users.

## Technical Specifications

### Automated Email & Reminder Logic
The system includes a dedicated `SharedLogic` service that manages complex asynchronous tasks:
- **Notification Service:** Integrated with SMTP to send real-time updates for booking confirmations and payment receipts.
- **5-Day Trip Reminders:** Specialized logic triggered by the Admin to scan for upcoming departures and notify users 5 days prior.
- **Reservation Expiry:** Automated cleanup of expired "Pending Payment" reservations, ensuring rooms are returned to inventory and the next person on the waiting list is notified.

### Security and Compliance
- **SSL/HTTPS Protocol:** Mandatory encryption implemented for all payment and checkout processes.
- **Data Protection:** Credit card information is processed for transactions but is never stored in the database.
- **SQL Injection Prevention:** Utilization of parameterized queries and ADO.NET best practices to secure all database interactions.

### Technology Stack
-**Framework:** .NET Core 6+ (MVC Architecture) 
- **Database:** Oracle Database (Relational)
- **Backend:** C#
- **Frontend:** HTML5, CSS3, JavaScript, Razor Pages

## Database Design
The application is backed by a normalized relational database consisting of the following core tables:
- **ADMIN:** Stores administrative credentials and site management data.
- **USERS:** User profiles and authentication details.
- **TRIPS:** General travel package definitions (destinations, descriptions, categories).
- **TRIP_INSTANCES:** Specific scheduled departures for trips, including availability and pricing.
- **ORDERS:** Tracking of all bookings, payment statuses, and history].
- **CART:** Temporary storage for user selections before checkout.
- **TRIP_REVIEWS:** Feedback and ratings specifically for travel packages.
- **SITE_REVIEWS:** Overall service experience ratings displayed on the main page.


## Installation and Execution
1. Ensure the Oracle Instant Client is configured on the host machine.
2. Update the `ConnectionStrings` in the `appsettings.json` file with valid Oracle credentials.
3. Configure SMTP settings in the `SendStatusEmail` method for mail functionality.
4. Build and run the solution via Visual Studio or the .NET CLI:
   ```bash
   dotnet run
