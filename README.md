# Trading System Project

This is my full stack trading management project on trading system that demonstrates how to manage inventory,sales,purchases,reports,and KPIs 

i've used SQL Server, ASP.NET Core Minimal APIs, Dapper and SSRS


Tech Stack --

Backend:
ASP.NET Core 8 Minimal APIs
Dapper (micro ORM for SQL queries)
JWT Authentication & Authorization
SQL Server (Stored Procedures, Views, Triggers)

Reports:
SSRS (SQL Server Reporting Services) with shared data sources
Report Designer in Visual Studio

Database:
SQL Server (Trading database)
Tables for Items, Vendors, Customers, PurchaseOrders, SalesOrders, InventoryLedger, etc.
Stored Procedures (usp_PostReceipt, usp_PostShipment, usp_SalesTotals, usp_TopItemsByMargin)

Features --

Authentication & Authorization:
Users login with username/password
JWT tokens are issued
Role-based policies (Admin, Ops)

Items Management:
Search, paginate, and list items (/api/items)
Get live stock levels (/api/items/{id}/stock)

Purchase & Sales Flow:
Post purchase receipts (usp_PostReceipt)
Post sales shipments (usp_PostShipment)
Ledger updates automatically

KPI & Analytics:
/api/kpi/sales → Returns revenue, cost, margin, top items
SSRS reports for interactive dashboards

Database Layer:
SQL Server with proper schema
Stored procedures for transactional safety
Views for simplified reporting (vw_StockOnHand)

Reports:
SSRS integrated with Visual Studio
Shared Data Source (TradingDB_DS)
Reports for inventory, sales, and KPIs


📊 Workflow Summary

Database Setup:
Created Trading database in SSMS
Designed schema (Items, Orders, Vendors, Customers, Ledger)
Wrote stored procedures for core operations
Seeded database with initial test data

API Development:
Built Program.cs using Minimal APIs
Configured JWT authentication & roles
Added endpoints for items, stock, receipts, shipments, KPIs
Centralized SQL error handling

Reporting (SSRS):
Configured shared data source (TradingDB_DS)
Built reports (TradingReports.rdl, SalesReport.rdl, InventoryReport.rdl)
Connected to Trading DB and stored procedures
Testing & Deployment

Tested APIs in Swagger UI:
Verified reports in Report Designer
Ready to publish on GitHub for demo/presentation
This repository contains a demo Trading System project with SQL database, SSRS reports, and API.

## Structure
- Database (SQL scripts)
- Reports (SSRS .rdl files)
- API (C# code)
- Docs (Documentation)
