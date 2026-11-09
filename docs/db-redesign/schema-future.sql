-- =====================================================================
-- CS 308 Online Store - FUTURE extensions (NOT part of schema.sql)
-- Run AFTER schema.sql on the same database. Each block is independent unless noted.
-- Kept here so the team can add them later without redesigning; none is required by the PDF.
-- =====================================================================

-- ---------------------------------------------------------------------
-- Extension E1: ProductGroups ("other colours / storage sizes" link, Trendyol style)
-- Products stay independent rows; GroupId only lets the UI show sibling products.
-- ---------------------------------------------------------------------
CREATE TABLE ProductGroups (
  Id        BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  Name      VARCHAR(200) NOT NULL,
  CreatedAt DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  PRIMARY KEY (Id),
  UNIQUE KEY UX_ProductGroups_Name (Name)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

ALTER TABLE Products
  ADD COLUMN GroupId BIGINT UNSIGNED NULL AFTER DistributorId,
  ADD KEY IX_Products_Group (GroupId),
  ADD CONSTRAINT FK_Products_ProductGroups FOREIGN KEY (GroupId) REFERENCES ProductGroups (Id) ON DELETE RESTRICT;

-- ---------------------------------------------------------------------
-- Extension E2: VAT (KDV). Adds TaxRates and the VAT breakdown on orders and invoices.
-- ---------------------------------------------------------------------
-- [EXT] VAT rates (KDV). Why: invoice and profit reports need the tax part of tax-inclusive prices.
CREATE TABLE TaxRates (
  Id       BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  Name     VARCHAR(50) NOT NULL,
  Rate     DECIMAL(5,4) NOT NULL,
  IsActive TINYINT(1) NOT NULL DEFAULT 1,
  PRIMARY KEY (Id),
  UNIQUE KEY UX_TaxRates_Name (Name),
  CONSTRAINT CK_TaxRates_Rate CHECK (Rate >= 0 AND Rate < 1)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

ALTER TABLE Products
  ADD COLUMN TaxRateId BIGINT UNSIGNED NULL AFTER DistributorId,
  ADD CONSTRAINT FK_Products_TaxRates FOREIGN KEY (TaxRateId) REFERENCES TaxRates (Id) ON DELETE RESTRICT;

ALTER TABLE OrderItems
  ADD COLUMN TaxRate   DECIMAL(5,4)  NOT NULL DEFAULT 0 AFTER DiscountRate,
  ADD COLUMN TaxAmount DECIMAL(18,2) NOT NULL DEFAULT 0 AFTER TaxRate,
  ADD CONSTRAINT CK_OrderItems_TaxRate CHECK (TaxRate >= 0 AND TaxRate < 1 AND TaxAmount >= 0);

ALTER TABLE Orders   ADD COLUMN TaxTotal DECIMAL(18,2) NOT NULL DEFAULT 0 AFTER DiscountTotal,
                     ADD CONSTRAINT CK_Orders_TaxTotal CHECK (TaxTotal >= 0);
ALTER TABLE Invoices ADD COLUMN TaxTotal DECIMAL(18,2) NOT NULL DEFAULT 0 AFTER DiscountTotal,
                     ADD CONSTRAINT CK_Invoices_TaxTotal CHECK (TaxTotal >= 0);

-- ---------------------------------------------------------------------
-- Extension E3: security and session tracking
-- ---------------------------------------------------------------------
-- [EXT] Refresh/session tokens. Why: revoke one device without touching the others;
-- only the SHA-256 of the token is stored. SecurityStamp is copied at issue time.
CREATE TABLE UserSessions (
  Id            BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  UserId        BIGINT UNSIGNED NOT NULL,
  TokenHash     BINARY(32) NOT NULL,
  SecurityStamp CHAR(32) NOT NULL,
  IpHash        BINARY(32) NULL,
  UserAgent     VARCHAR(255) NULL,
  CreatedAt     DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  ExpiresAt     DATETIME(6) NOT NULL,
  RevokedAt     DATETIME(6) NULL,
  PRIMARY KEY (Id),
  UNIQUE KEY UX_UserSessions_Token (TokenHash),
  KEY IX_UserSessions_UserActive (UserId, RevokedAt, ExpiresAt),
  CONSTRAINT FK_UserSessions_Users FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE CASCADE,
  CONSTRAINT CK_UserSessions_Expiry CHECK (ExpiresAt > CreatedAt)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- [EXT] Login attempts. Why: lockout and abuse analysis; keyed by the e-mail blind index
-- so unknown accounts can be counted too. Append-only.
CREATE TABLE LoginAttempts (
  Id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  EmailBlindIndex BINARY(32) NOT NULL,
  UserId          BIGINT UNSIGNED NULL,
  Succeeded       TINYINT(1) NOT NULL,
  IpHash          BINARY(32) NULL,
  AttemptedAt     DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  PRIMARY KEY (Id),
  KEY IX_LoginAttempts_Email (EmailBlindIndex, AttemptedAt),
  CONSTRAINT FK_LoginAttempts_Users FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- [EXT] Consent log (KVKK / marketing). Why: legal proof of what was accepted and when.
-- Append-only: the current consent is the latest row per (user, type).
CREATE TABLE ConsentRecords (
  Id            BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  UserId        BIGINT UNSIGNED NOT NULL,
  ConsentType   VARCHAR(20) COLLATE utf8mb4_bin NOT NULL,
  Granted       TINYINT(1) NOT NULL,
  PolicyVersion VARCHAR(20) NOT NULL,
  RecordedAt    DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  PRIMARY KEY (Id),
  KEY IX_ConsentRecords_Latest (UserId, ConsentType, RecordedAt),
  CONSTRAINT FK_ConsentRecords_Users FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE RESTRICT,
  CONSTRAINT CK_ConsentRecords_Type CHECK (ConsentType IN ('KVKK','Marketing'))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ---------------------------------------------------------------------
-- Extension E4: history and audit (order status history, stock ledger, price history, audit log)
-- ---------------------------------------------------------------------
-- [EXT] Order status history. Why: the order history page (PDF 3) can show when each state began.
CREATE TABLE OrderStatusHistory (
  Id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  OrderId         BIGINT UNSIGNED NOT NULL,
  FromStatus      VARCHAR(20) COLLATE utf8mb4_bin NULL,
  ToStatus        VARCHAR(20) COLLATE utf8mb4_bin NOT NULL,
  ChangedByUserId BIGINT UNSIGNED NULL,
  Note            VARCHAR(500) NULL,
  ChangedAt       DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  PRIMARY KEY (Id),
  KEY IX_OrderStatusHistory_Order (OrderId, ChangedAt),
  KEY IX_OrderStatusHistory_User (ChangedByUserId),
  CONSTRAINT FK_OrderStatusHistory_Orders FOREIGN KEY (OrderId) REFERENCES Orders (Id) ON DELETE RESTRICT,
  CONSTRAINT FK_OrderStatusHistory_Users FOREIGN KEY (ChangedByUserId) REFERENCES Users (Id) ON DELETE RESTRICT,
  CONSTRAINT CK_OrderStatusHistory_Status CHECK (
    ToStatus IN ('Processing','InTransit','Delivered','Cancelled') AND
    (FromStatus IS NULL OR FromStatus IN ('Processing','InTransit','Delivered','Cancelled')))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- [EXT] Stock movement ledger (single warehouse). Why: answers "why did this stock change"
-- (PDF 12 puts stock under the ProductManager). Products.StockQuantity stays the
-- fast current value; every change inserts a ledger row in the same transaction.
-- Unique (OrderItemId, Reason) and unique ReturnRequestId = stock restored exactly once.
CREATE TABLE StockMovements (
  Id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  ProductId       BIGINT UNSIGNED NOT NULL,
  Delta           INT NOT NULL,
  Reason          VARCHAR(20) COLLATE utf8mb4_bin NOT NULL,
  OrderItemId     BIGINT UNSIGNED NULL,
  ReturnRequestId BIGINT UNSIGNED NULL,
  ActorUserId     BIGINT UNSIGNED NULL,
  Note            VARCHAR(500) NULL,
  CreatedAt       DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  PRIMARY KEY (Id),
  UNIQUE KEY UX_StockMovements_OrderItemReason (OrderItemId, Reason),
  UNIQUE KEY UX_StockMovements_ReturnRequest (ReturnRequestId),
  KEY IX_StockMovements_Product (ProductId, CreatedAt),
  KEY IX_StockMovements_Actor (ActorUserId),
  CONSTRAINT FK_StockMovements_Products FOREIGN KEY (ProductId) REFERENCES Products (Id) ON DELETE RESTRICT,
  CONSTRAINT FK_StockMovements_OrderItems FOREIGN KEY (OrderItemId) REFERENCES OrderItems (Id) ON DELETE RESTRICT,
  CONSTRAINT FK_StockMovements_ReturnRequests FOREIGN KEY (ReturnRequestId) REFERENCES ReturnRequests (Id) ON DELETE RESTRICT,
  CONSTRAINT FK_StockMovements_Users FOREIGN KEY (ActorUserId) REFERENCES Users (Id) ON DELETE RESTRICT,
  CONSTRAINT CK_StockMovements_Reason CHECK (
    (Reason = 'Sale'       AND Delta < 0 AND OrderItemId IS NOT NULL AND ReturnRequestId IS NULL) OR
    (Reason = 'Cancel'     AND Delta > 0 AND OrderItemId IS NOT NULL AND ReturnRequestId IS NULL) OR
    (Reason = 'Return'     AND Delta > 0 AND ReturnRequestId IS NOT NULL AND OrderItemId IS NULL) OR
    (Reason IN ('Receipt','Adjustment') AND Delta <> 0 AND OrderItemId IS NULL AND ReturnRequestId IS NULL AND ActorUserId IS NOT NULL))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- [EXT] Base price history. Why: shows who changed a price and when (price audit and charts).
-- Discounts do not rewrite Price: the discounted price is computed from Discounts.
CREATE TABLE ProductPriceHistory (
  Id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  ProductId       BIGINT UNSIGNED NOT NULL,
  OldPrice        DECIMAL(18,2) NOT NULL,
  NewPrice        DECIMAL(18,2) NOT NULL,
  ChangedByUserId BIGINT UNSIGNED NOT NULL,
  Reason          VARCHAR(200) NULL,
  ChangedAt       DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  PRIMARY KEY (Id),
  KEY IX_ProductPriceHistory_Product (ProductId, ChangedAt),
  KEY IX_ProductPriceHistory_User (ChangedByUserId),
  CONSTRAINT FK_ProductPriceHistory_Products FOREIGN KEY (ProductId) REFERENCES Products (Id) ON DELETE RESTRICT,
  CONSTRAINT FK_ProductPriceHistory_Users FOREIGN KEY (ChangedByUserId) REFERENCES Users (Id) ON DELETE RESTRICT,
  CONSTRAINT CK_ProductPriceHistory_Changed CHECK (OldPrice >= 0 AND NewPrice >= 0 AND OldPrice <> NewPrice)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- [EXT] Audit log of sensitive changes (price, stock, role, status). Append-only: the
-- application account gets INSERT and SELECT only. EntityType/EntityId deliberately are NOT
-- foreign keys: a log must outlive and name any row, and this is the only table that does so.
-- JSON is used here only for the before/after document of a change, not for relational data.
CREATE TABLE AuditLogs (
  Id          BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  OccurredAt  DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  ActorUserId BIGINT UNSIGNED NULL,
  ActorRole   VARCHAR(20) NULL,
  Action      VARCHAR(50) NOT NULL,
  EntityType  VARCHAR(50) NOT NULL,
  EntityId    BIGINT UNSIGNED NOT NULL,
  OldValues   JSON NULL,
  NewValues   JSON NULL,
  RequestId   CHAR(36) NULL,
  PRIMARY KEY (Id),
  KEY IX_AuditLogs_Entity (EntityType, EntityId, OccurredAt),
  KEY IX_AuditLogs_Actor (ActorUserId, OccurredAt),
  CONSTRAINT FK_AuditLogs_Users FOREIGN KEY (ActorUserId) REFERENCES Users (Id) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ---------------------------------------------------------------------
-- Extension E5: delivery tracking
-- ---------------------------------------------------------------------
-- [EXT] Carriers. Why: the delivery list can show who carries the parcel and a tracking link.
CREATE TABLE Carriers (
  Id                 BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  Name               VARCHAR(100) NOT NULL,
  TrackingUrlTemplate VARCHAR(500) NULL,
  IsActive           TINYINT(1) NOT NULL DEFAULT 1,
  PRIMARY KEY (Id),
  UNIQUE KEY UX_Carriers_Name (Name)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

ALTER TABLE Deliveries
  ADD COLUMN CarrierId BIGINT UNSIGNED NULL AFTER Status,
  ADD COLUMN TrackingNumber VARCHAR(64) NULL AFTER CarrierId,
  ADD KEY IX_Deliveries_Carrier (CarrierId),
  ADD CONSTRAINT FK_Deliveries_Carriers FOREIGN KEY (CarrierId) REFERENCES Carriers (Id) ON DELETE RESTRICT,
  ADD CONSTRAINT CK_Deliveries_Tracking CHECK (TrackingNumber IS NULL OR CarrierId IS NOT NULL);

-- [EXT] Tracking events of a delivery. Append-only; shown on the customer's order page.
CREATE TABLE DeliveryTrackingEvents (
  Id          BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  DeliveryId  BIGINT UNSIGNED NOT NULL,
  EventType   VARCHAR(20) COLLATE utf8mb4_bin NOT NULL,
  Location    VARCHAR(150) NULL,
  Description VARCHAR(300) NULL,
  OccurredAt  DATETIME(6) NOT NULL,
  CreatedAt   DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  PRIMARY KEY (Id),
  KEY IX_DeliveryTrackingEvents_Delivery (DeliveryId, OccurredAt),
  CONSTRAINT FK_DeliveryTrackingEvents_Deliveries FOREIGN KEY (DeliveryId) REFERENCES Deliveries (Id) ON DELETE RESTRICT,
  CONSTRAINT CK_DeliveryTrackingEvents_Type CHECK (EventType IN ('LabelCreated','PickedUp','InTransit','OutForDelivery','Delivered','Exception'))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ---------------------------------------------------------------------
-- Extension E6: precomputed reporting (use only if live queries become slow)
-- ---------------------------------------------------------------------
-- [EXT] Ready-made rating aggregate per product (count and sum; the average is derived).
-- Consistency: updated in the same transaction as the rating insert/update with an atomic
-- "SET RatingSum = RatingSum + delta"; vw_RatingStatsCheck shows any drift.
CREATE TABLE ProductRatingStats (
  ProductId   BIGINT UNSIGNED NOT NULL,
  RatingCount INT UNSIGNED NOT NULL DEFAULT 0,
  RatingSum   INT UNSIGNED NOT NULL DEFAULT 0,
  UpdatedAt   DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
  RatingAverage DECIMAL(4,3) GENERATED ALWAYS AS (IF(RatingCount = 0, NULL, RatingSum / RatingCount)) VIRTUAL,
  PRIMARY KEY (ProductId),
  CONSTRAINT FK_ProductRatingStats_Products FOREIGN KEY (ProductId) REFERENCES Products (Id) ON DELETE RESTRICT,
  CONSTRAINT CK_ProductRatingStats_Range CHECK (RatingSum >= RatingCount AND RatingSum <= RatingCount * 5)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- [EXT] Popularity counters per product (PDF 7 sort by popularity).
-- NetSold = sold minus refunded quantity, stored and indexed with ProductId as the
-- deterministic tiebreaker. Updated in the checkout / refund transactions;
-- vw_ProductSalesCheck recomputes the truth from OrderItems for reconciliation.
CREATE TABLE ProductSalesStats (
  ProductId        BIGINT UNSIGNED NOT NULL,
  SoldQuantity     BIGINT NOT NULL DEFAULT 0,
  RefundedQuantity BIGINT NOT NULL DEFAULT 0,
  UpdatedAt        DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
  NetSold          BIGINT GENERATED ALWAYS AS (SoldQuantity - RefundedQuantity) STORED,
  PRIMARY KEY (ProductId),
  KEY IX_ProductSalesStats_Popularity (NetSold, ProductId),
  CONSTRAINT FK_ProductSalesStats_Products FOREIGN KEY (ProductId) REFERENCES Products (Id) ON DELETE RESTRICT,
  CONSTRAINT CK_ProductSalesStats_Range CHECK (SoldQuantity >= 0 AND RefundedQuantity >= 0 AND RefundedQuantity <= SoldQuantity)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- [EXT] Daily finance summary (UTC day) for the revenue / profit chart (PDF 11).
-- Rebuilt per day by a job from OrderItems and Refunds; reports for short ranges can query
-- the facts directly. Net values are computed columns.
CREATE TABLE DailyFinancialSummaries (
  SummaryDate     DATE NOT NULL,
  OrderCount      INT UNSIGNED NOT NULL DEFAULT 0,
  GrossRevenue    DECIMAL(18,2) NOT NULL DEFAULT 0,
  GrossCost       DECIMAL(18,2) NOT NULL DEFAULT 0,
  RefundedRevenue DECIMAL(18,2) NOT NULL DEFAULT 0,
  RefundedCost    DECIMAL(18,2) NOT NULL DEFAULT 0,
  UpdatedAt       DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
  NetRevenue      DECIMAL(18,2) GENERATED ALWAYS AS (GrossRevenue - RefundedRevenue) VIRTUAL,
  NetProfit       DECIMAL(18,2) GENERATED ALWAYS AS ((GrossRevenue - RefundedRevenue) - (GrossCost - RefundedCost)) VIRTUAL,
  PRIMARY KEY (SummaryDate),
  CONSTRAINT CK_DailyFinancialSummaries_Money CHECK (GrossRevenue >= 0 AND GrossCost >= 0 AND RefundedRevenue >= 0 AND RefundedCost >= 0 AND RefundedRevenue <= GrossRevenue)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- Reconciliation: the true sold/refunded quantity per product from facts, to compare
-- with ProductSalesStats.
CREATE VIEW vw_ProductSalesCheck AS
SELECT oi.ProductId,
       SUM(oi.Quantity) AS SoldQuantity,
       SUM(oi.RefundedQuantity) AS RefundedQuantity
FROM OrderItems oi
JOIN Orders o ON o.Id = oi.OrderId
WHERE o.PaymentStatus IN ('Confirmed','Refunded') AND o.Status <> 'Cancelled'
GROUP BY oi.ProductId;

-- Reconciliation: the true rating count/sum per product, to compare with ProductRatingStats.
CREATE VIEW vw_RatingStatsCheck AS
SELECT r.ProductId, COUNT(*) AS RatingCount, SUM(r.Stars) AS RatingSum
FROM Ratings r
GROUP BY r.ProductId;
