-- =====================================================================
-- CS 308 Online Store - target schema proposal (v2: one row per product)
-- Target: MySQL 8.4, InnoDB, utf8mb4 / utf8mb4_0900_ai_ci, STRICT mode.
-- Run against an EMPTY database:  mysql --default-character-set=utf8mb4 -D <db> < schema.sql
-- Connections must use time_zone = '+00:00' (all DATETIME(6) values are UTC).
--
-- 29 tables: 25 [CORE] (required by a PDF requirement, number in brackets) and
-- 4 [EXT, kept] catalog tables that cover the "more diverse electronics" feedback.
-- Other ideas live in schema-future.sql and are NOT part of this schema.
--
-- Conventions
--   * Tables and columns: PascalCase plural, as in the current EF model.
--   * Internal keys: BIGINT UNSIGNED AUTO_INCREMENT. Public identifiers (PublicId,
--     OrderNumber, InvoiceNumber) are separate columns, never the internal key.
--   * Status columns: VARCHAR + CHECK, collation utf8mb4_bin (case-sensitive values).
--   * Money: DECIMAL(18,2), TRY only (CurrencyCode CHECK = 'TRY' where money is stored).
--     Prices include VAT; a VAT breakdown is a future extension (see schema-future.sql).
--   * Encrypted columns (AES-GCM in the application) are VARBINARY, with DataKeyVersion.
--     Keys are never stored in the database.
--   * History tables use ON DELETE RESTRICT. CASCADE is used only for owned drafts.
--   * Composite foreign keys ("the line belongs to the same order / user / product")
--     need EXACT unique keys on the parent: MySQL 8.4 refuses non-unique FK targets.
-- =====================================================================

-- ---------------------------------------------------------------------
-- 1. IDENTITY
-- ---------------------------------------------------------------------

-- [CORE 10, 14, 17] One account table for customers and staff (four fixed roles).
-- Why: PDF 10 fixes the four roles; PDF 14 lists the customer fields; PDF 17 requires
-- protected personal data. Email is encrypted; lookups use EmailBlindIndex (HMAC-SHA256).
CREATE TABLE Users (
  Id               BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  PublicId         CHAR(36) NOT NULL,
  Name             VARCHAR(100) NOT NULL,
  EmailEnc         VARBINARY(512) NOT NULL,
  EmailBlindIndex  BINARY(32) NOT NULL,
  TaxIdEnc         VARBINARY(256) NULL,
  DataKeyVersion   SMALLINT UNSIGNED NOT NULL,
  PasswordHash     VARCHAR(255) NOT NULL,
  Role             VARCHAR(20) COLLATE utf8mb4_bin NOT NULL DEFAULT 'Customer',
  SecurityStamp    CHAR(32) NOT NULL,
  IsActive         TINYINT(1) NOT NULL DEFAULT 1,
  FailedLoginCount SMALLINT UNSIGNED NOT NULL DEFAULT 0,
  LockoutUntil     DATETIME(6) NULL,
  CreatedAt        DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  UpdatedAt        DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  RowVersion       DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
  PRIMARY KEY (Id),
  UNIQUE KEY UX_Users_PublicId (PublicId),
  UNIQUE KEY UX_Users_EmailBlindIndex (EmailBlindIndex),
  CONSTRAINT CK_Users_Role CHECK (Role IN ('Customer','SalesManager','ProductManager','SupportAgent')),
  CONSTRAINT CK_Users_Name CHECK (CHAR_LENGTH(TRIM(Name)) > 0),
  CONSTRAINT CK_Users_KeyVersion CHECK (DataKeyVersion > 0),
  CONSTRAINT CK_Users_CustomerTaxId CHECK (Role <> 'Customer' OR TaxIdEnc IS NOT NULL)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- [CORE 14] Several delivery addresses per customer (home address = the default one).
-- Why: PDF 14 needs a home address; multiple addresses are cheap and let checkout pick one.
-- At most one default address per user is enforced by the unique virtual column.
CREATE TABLE Addresses (
  Id             BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  UserId         BIGINT UNSIGNED NOT NULL,
  Label          VARCHAR(50) NOT NULL,
  AddressEnc     VARBINARY(2048) NOT NULL,
  DataKeyVersion SMALLINT UNSIGNED NOT NULL,
  IsDefault      TINYINT(1) NOT NULL DEFAULT 0,
  IsActive       TINYINT(1) NOT NULL DEFAULT 1,
  CreatedAt      DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  UpdatedAt      DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  DefaultOwnerId BIGINT UNSIGNED GENERATED ALWAYS AS (IF(IsDefault = 1 AND IsActive = 1, UserId, NULL)) VIRTUAL,
  PRIMARY KEY (Id),
  UNIQUE KEY UX_Addresses_OneDefault (DefaultOwnerId),
  KEY IX_Addresses_User (UserId),
  CONSTRAINT FK_Addresses_Users FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE CASCADE,
  CONSTRAINT CK_Addresses_KeyVersion CHECK (DataKeyVersion > 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;


-- ---------------------------------------------------------------------
-- 2. CATALOG
-- ---------------------------------------------------------------------

-- [EXT, kept] Brands. Why: filter and display for a broader electronics catalog.
CREATE TABLE Brands (
  Id        BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  Name      VARCHAR(100) NOT NULL,
  IsActive  TINYINT(1) NOT NULL DEFAULT 1,
  CreatedAt DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  PRIMARY KEY (Id),
  UNIQUE KEY UX_Brands_Name (Name)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- [CORE 1, 12] Category tree (adjacency list, depth limit is a service rule).
-- Why: PDF 1 needs categories; depth is 2-3 for electronics, so ParentId is enough.
-- ParentKey makes (parent, name) unique even for root categories (NULL parent).
-- A category being its own parent / cycles are prevented by the service (a CHECK cannot read
-- the AUTO_INCREMENT Id in MySQL).
CREATE TABLE Categories (
  Id          BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  ParentId    BIGINT UNSIGNED NULL,
  Name        VARCHAR(100) NOT NULL,
  Slug        VARCHAR(120) NOT NULL,
  Description TEXT NULL,
  SortOrder   SMALLINT UNSIGNED NOT NULL DEFAULT 0,
  IsActive    TINYINT(1) NOT NULL DEFAULT 1,
  CreatedAt   DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  ParentKey   BIGINT UNSIGNED GENERATED ALWAYS AS (IFNULL(ParentId, 0)) VIRTUAL,
  PRIMARY KEY (Id),
  UNIQUE KEY UX_Categories_Slug (Slug),
  UNIQUE KEY UX_Categories_ParentName (ParentKey, Name),
  CONSTRAINT FK_Categories_Parent FOREIGN KEY (ParentId) REFERENCES Categories (Id) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- [CORE 9] Distributors. Why: PDF 9 requires distributor information on every product;
-- a table avoids repeating contact text on each product.
CREATE TABLE Distributors (
  Id          BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  Name        VARCHAR(150) NOT NULL,
  ContactInfo VARCHAR(500) NOT NULL,
  IsActive    TINYINT(1) NOT NULL DEFAULT 1,
  CreatedAt   DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  PRIMARY KEY (Id),
  UNIQUE KEY UX_Distributors_Name (Name)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- [CORE 1, 3, 7, 9, 11] Product = the sellable item (one row per sellable thing).
-- Why: PDF 9 lists the product fields (ID, name, model, serial number, description, stock,
-- price, warranty, distributor); PDF 3 needs stock per product; PDF 11 needs price and cost.
-- A 256 GB and a 512 GB phone are two products (Trendyol style), each with its own price/stock.
-- Cost NULL means "50% of the paid price" (PDF 11); checkout snapshots the result.
-- StockQuantity is UNSIGNED: a decrement below zero fails in STRICT mode (last-unit race).
-- RowVersion is the optimistic-concurrency token for stock and price edits.
-- Search (PDF 7): FULLTEXT on SearchText, a STORED computed copy of name + model + description
-- in which Turkish dotless/dotted i ('ı', 'İ') is replaced by 'i'. The default collation already
-- ignores case and accents (Şarj = sarj) but treats 'ı' and 'i' as different letters, so the
-- application must apply the same replacement to the user's search text. Tokens shorter than 3
-- characters (for example the model 'X1') are not indexed: use a LIKE fallback for those.
-- Stock is NOT part of the search, so out-of-stock products stay searchable.
CREATE TABLE Products (
  Id             BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  CategoryId     BIGINT UNSIGNED NOT NULL,
  BrandId        BIGINT UNSIGNED NULL,
  DistributorId  BIGINT UNSIGNED NOT NULL,
  Sku            VARCHAR(64) NOT NULL,
  Name           VARCHAR(200) NOT NULL,
  Model          VARCHAR(100) NOT NULL,
  SerialNumber   VARCHAR(100) NOT NULL,
  Description    TEXT NOT NULL,
  WarrantyStatus VARCHAR(100) NOT NULL,
  Price          DECIMAL(18,2) NOT NULL,
  Cost           DECIMAL(18,2) NULL,
  StockQuantity  INT UNSIGNED NOT NULL DEFAULT 0,
  IsActive       TINYINT(1) NOT NULL DEFAULT 1,
  CreatedAt      DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  UpdatedAt      DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  RowVersion     DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
  SearchText     TEXT GENERATED ALWAYS AS (REPLACE(REPLACE(CONCAT_WS(' ', Name, Model, Description), 'ı', 'i'), 'İ', 'i')) STORED,
  PRIMARY KEY (Id),
  UNIQUE KEY UX_Products_Sku (Sku),
  KEY IX_Products_CategoryList (CategoryId, IsActive, Name),
  KEY IX_Products_PriceSort (IsActive, Price),
  KEY IX_Products_Brand (BrandId),
  KEY IX_Products_Distributor (DistributorId),
  FULLTEXT KEY FT_Products_Search (SearchText),
  CONSTRAINT FK_Products_Categories FOREIGN KEY (CategoryId) REFERENCES Categories (Id) ON DELETE RESTRICT,
  CONSTRAINT FK_Products_Brands FOREIGN KEY (BrandId) REFERENCES Brands (Id) ON DELETE RESTRICT,
  CONSTRAINT FK_Products_Distributors FOREIGN KEY (DistributorId) REFERENCES Distributors (Id) ON DELETE RESTRICT,
  CONSTRAINT CK_Products_Price CHECK (Price >= 0),
  CONSTRAINT CK_Products_Cost CHECK (Cost IS NULL OR Cost >= 0),
  CONSTRAINT CK_Products_ActiveDetails CHECK (IsActive = 0 OR (
    CHAR_LENGTH(TRIM(Sku)) > 0 AND CHAR_LENGTH(TRIM(Name)) > 0 AND CHAR_LENGTH(TRIM(Model)) > 0 AND
    CHAR_LENGTH(TRIM(SerialNumber)) > 0 AND CHAR_LENGTH(TRIM(Description)) > 0 AND
    CHAR_LENGTH(TRIM(WarrantyStatus)) > 0))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- [EXT, kept] Attribute definitions per category (typed technical specs).
-- Why: monitors, headphones and phones have different specs (resolution, connection, RAM).
-- Instead of one JSON blob, each spec has a declared type, so values are validated and
-- filterable. A definition belongs to a category and applies to its whole subtree
-- (the subtree lookup is a service/query rule).
CREATE TABLE AttributeDefinitions (
  Id           BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  CategoryId   BIGINT UNSIGNED NOT NULL,
  Code         VARCHAR(50) NOT NULL,
  Label        VARCHAR(100) NOT NULL,
  DataType     VARCHAR(10) COLLATE utf8mb4_bin NOT NULL,
  Unit         VARCHAR(20) NULL,
  IsFilterable TINYINT(1) NOT NULL DEFAULT 0,
  SortOrder    SMALLINT UNSIGNED NOT NULL DEFAULT 0,
  PRIMARY KEY (Id),
  UNIQUE KEY UX_AttributeDefinitions_Code (CategoryId, Code),
  UNIQUE KEY UX_AttributeDefinitions_Typed (Id, DataType),
  CONSTRAINT FK_AttributeDefinitions_Categories FOREIGN KEY (CategoryId) REFERENCES Categories (Id) ON DELETE RESTRICT,
  CONSTRAINT CK_AttributeDefinitions_DataType CHECK (DataType IN ('Text','Number','Bool'))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- [EXT, kept] Spec values of a product. Exactly one typed value column is filled, and the
-- composite FK forces it to match the DataType declared by the definition.
CREATE TABLE ProductAttributeValues (
  ProductId             BIGINT UNSIGNED NOT NULL,
  AttributeDefinitionId BIGINT UNSIGNED NOT NULL,
  DataType              VARCHAR(10) COLLATE utf8mb4_bin NOT NULL,
  ValueText             VARCHAR(255) NULL,
  ValueNumber           DECIMAL(18,4) NULL,
  ValueBool             TINYINT(1) NULL,
  PRIMARY KEY (ProductId, AttributeDefinitionId),
  KEY IX_ProductAttributeValues_Number (AttributeDefinitionId, ValueNumber),
  KEY IX_ProductAttributeValues_Text (AttributeDefinitionId, ValueText),
  CONSTRAINT FK_ProductAttributeValues_Products FOREIGN KEY (ProductId) REFERENCES Products (Id) ON DELETE RESTRICT,
  CONSTRAINT FK_ProductAttributeValues_Definitions FOREIGN KEY (AttributeDefinitionId, DataType)
    REFERENCES AttributeDefinitions (Id, DataType) ON DELETE RESTRICT,
  CONSTRAINT CK_ProductAttributeValues_Typed CHECK (
    (DataType = 'Text'   AND ValueText IS NOT NULL AND ValueNumber IS NULL AND ValueBool IS NULL) OR
    (DataType = 'Number' AND ValueNumber IS NOT NULL AND ValueText IS NULL AND ValueBool IS NULL) OR
    (DataType = 'Bool'   AND ValueBool IS NOT NULL AND ValueText IS NULL AND ValueNumber IS NULL))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- [EXT, kept] Product images. Why: a catalog with many product types needs more than one
-- picture per product. Files live in object storage; only the key is stored.
-- One primary image per product (unique virtual column, NULL for non-primary rows).
CREATE TABLE ProductImages (
  Id         BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  ProductId  BIGINT UNSIGNED NOT NULL,
  StorageKey VARCHAR(512) NOT NULL,
  AltText    VARCHAR(200) NULL,
  SortOrder  SMALLINT UNSIGNED NOT NULL DEFAULT 0,
  IsPrimary  TINYINT(1) NOT NULL DEFAULT 0,
  CreatedAt  DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  PrimaryProductId BIGINT UNSIGNED GENERATED ALWAYS AS (IF(IsPrimary = 1, ProductId, NULL)) VIRTUAL,
  PRIMARY KEY (Id),
  UNIQUE KEY UX_ProductImages_OnePrimary (PrimaryProductId),
  KEY IX_ProductImages_Product (ProductId, SortOrder),
  CONSTRAINT FK_ProductImages_Products FOREIGN KEY (ProductId) REFERENCES Products (Id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;


-- ---------------------------------------------------------------------
-- 3. DISCOUNTS
-- ---------------------------------------------------------------------

-- [CORE 11] Discount campaign set by a SalesManager (rate + products; system computes price).
-- Overlapping campaigns: the highest single rate wins (computed at checkout, never stacked).
CREATE TABLE Discounts (
  Id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  Name            VARCHAR(100) NOT NULL,
  Rate            DECIMAL(5,4) NOT NULL,
  StartsAt        DATETIME(6) NOT NULL,
  EndsAt          DATETIME(6) NOT NULL,
  CreatedByUserId BIGINT UNSIGNED NOT NULL,
  CancelledAt     DATETIME(6) NULL,
  CreatedAt       DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  PRIMARY KEY (Id),
  KEY IX_Discounts_Active (StartsAt, EndsAt),
  KEY IX_Discounts_CreatedBy (CreatedByUserId),
  CONSTRAINT FK_Discounts_Users FOREIGN KEY (CreatedByUserId) REFERENCES Users (Id) ON DELETE RESTRICT,
  CONSTRAINT CK_Discounts_Rate CHECK (Rate > 0 AND Rate <= 1),
  CONSTRAINT CK_Discounts_Period CHECK (EndsAt > StartsAt)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- [CORE 11] Products covered by a discount.
CREATE TABLE DiscountProducts (
  DiscountId BIGINT UNSIGNED NOT NULL,
  ProductId  BIGINT UNSIGNED NOT NULL,
  PRIMARY KEY (DiscountId, ProductId),
  KEY IX_DiscountProducts_Product (ProductId),
  CONSTRAINT FK_DiscountProducts_Discounts FOREIGN KEY (DiscountId) REFERENCES Discounts (Id) ON DELETE RESTRICT,
  CONSTRAINT FK_DiscountProducts_Products FOREIGN KEY (ProductId) REFERENCES Products (Id) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;


-- ---------------------------------------------------------------------
-- 4. CART AND WISHLIST
-- ---------------------------------------------------------------------

-- [CORE 1, 4] Cart owned by a user or by a hashed guest token (exactly one).
-- OpenUserId makes "one open cart per user" a database rule (NULL when not open).
-- A guest token maps to one cart for good (unique hash); a new guest cart gets a new token.
CREATE TABLE Carts (
  Id             BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  UserId         BIGINT UNSIGNED NULL,
  GuestTokenHash CHAR(64) NULL,
  State          VARCHAR(20) COLLATE utf8mb4_bin NOT NULL DEFAULT 'Open',
  CreatedAt      DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  UpdatedAt      DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  RowVersion     DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
  OpenUserId     BIGINT UNSIGNED GENERATED ALWAYS AS (IF(State = 'Open', UserId, NULL)) VIRTUAL,
  PRIMARY KEY (Id),
  UNIQUE KEY UX_Carts_GuestToken (GuestTokenHash),
  UNIQUE KEY UX_Carts_OneOpenPerUser (OpenUserId),
  UNIQUE KEY UX_Carts_IdUser (Id, UserId),
  KEY IX_Carts_UserState (UserId, State),
  CONSTRAINT FK_Carts_Users FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE CASCADE,
  CONSTRAINT CK_Carts_State CHECK (State IN ('Open','CheckedOut','Abandoned')),
  CONSTRAINT CK_Carts_Owner CHECK ((UserId IS NOT NULL AND GuestTokenHash IS NULL) OR (UserId IS NULL AND GuestTokenHash IS NOT NULL))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- [CORE 1, 7] Cart lines. Adding to a cart never changes stock (checkout does).
-- An out-of-stock product is blocked by the service, because a CHECK cannot read another table.
CREATE TABLE CartItems (
  Id        BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  CartId    BIGINT UNSIGNED NOT NULL,
  ProductId BIGINT UNSIGNED NOT NULL,
  Quantity  INT UNSIGNED NOT NULL,
  AddedAt   DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  UpdatedAt DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  PRIMARY KEY (Id),
  UNIQUE KEY UX_CartItems_CartProduct (CartId, ProductId),
  KEY IX_CartItems_Product (ProductId),
  CONSTRAINT FK_CartItems_Carts FOREIGN KEY (CartId) REFERENCES Carts (Id) ON DELETE CASCADE,
  CONSTRAINT FK_CartItems_Products FOREIGN KEY (ProductId) REFERENCES Products (Id) ON DELETE RESTRICT,
  CONSTRAINT CK_CartItems_Quantity CHECK (Quantity > 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- [CORE 11, 14] Wishlist. Why: the discount notification (PDF 11) targets
-- the users whose wishlist contains the discounted product.
CREATE TABLE WishlistItems (
  UserId    BIGINT UNSIGNED NOT NULL,
  ProductId BIGINT UNSIGNED NOT NULL,
  AddedAt   DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  PRIMARY KEY (UserId, ProductId),
  KEY IX_WishlistItems_Product (ProductId),
  CONSTRAINT FK_WishlistItems_Users FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE CASCADE,
  CONSTRAINT FK_WishlistItems_Products FOREIGN KEY (ProductId) REFERENCES Products (Id) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;


-- ---------------------------------------------------------------------
-- 5. ORDERS AND PAYMENTS
-- ---------------------------------------------------------------------

-- [CORE 3, 4, 14] Order header with purchase-time snapshots and totals.
-- Why: PDF 3/4 need order status and history; snapshots keep invoices and reports stable.
-- SourceCartId is unique: the same cart cannot be bought twice with different keys.
-- Totals are tax-inclusive: GrandTotal = Subtotal - DiscountTotal + ShippingTotal.
CREATE TABLE Orders (
  Id                BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  OrderNumber       VARCHAR(24) NOT NULL,
  UserId            BIGINT UNSIGNED NOT NULL,
  SourceCartId      BIGINT UNSIGNED NULL,
  Status            VARCHAR(20) COLLATE utf8mb4_bin NOT NULL DEFAULT 'Processing',
  PaymentStatus     VARCHAR(20) COLLATE utf8mb4_bin NOT NULL DEFAULT 'Pending',
  CurrencyCode      CHAR(3) COLLATE utf8mb4_bin NOT NULL DEFAULT 'TRY',
  CustomerName      VARCHAR(100) NOT NULL,
  CustomerEmailEnc  VARBINARY(512) NOT NULL,
  CustomerTaxIdEnc  VARBINARY(256) NOT NULL,
  ShippingAddressEnc VARBINARY(2048) NOT NULL,
  DataKeyVersion    SMALLINT UNSIGNED NOT NULL,
  Subtotal          DECIMAL(18,2) NOT NULL,
  DiscountTotal     DECIMAL(18,2) NOT NULL DEFAULT 0,
  ShippingTotal     DECIMAL(18,2) NOT NULL DEFAULT 0,
  GrandTotal        DECIMAL(18,2) NOT NULL,
  CreatedAt         DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  PurchasedAt       DATETIME(6) NULL,
  DeliveredAt       DATETIME(6) NULL,
  CancelledAt       DATETIME(6) NULL,
  CancelReason      VARCHAR(500) NULL,
  RowVersion        DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
  PRIMARY KEY (Id),
  UNIQUE KEY UX_Orders_Number (OrderNumber),
  UNIQUE KEY UX_Orders_SourceCart (SourceCartId),
  UNIQUE KEY UX_Orders_IdUser (Id, UserId),
  KEY IX_Orders_UserHistory (UserId, CreatedAt),
  KEY IX_Orders_PurchasedAt (PurchasedAt),
  CONSTRAINT FK_Orders_Users FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE RESTRICT,
  CONSTRAINT FK_Orders_Carts FOREIGN KEY (SourceCartId) REFERENCES Carts (Id) ON DELETE RESTRICT,
  CONSTRAINT CK_Orders_Status CHECK (Status IN ('Processing','InTransit','Delivered','Cancelled')),
  CONSTRAINT CK_Orders_PaymentStatus CHECK (PaymentStatus IN ('Pending','Confirmed','Failed','Refunded')),
  CONSTRAINT CK_Orders_Currency CHECK (CurrencyCode = 'TRY'),
  CONSTRAINT CK_Orders_FulfillmentPayment CHECK (
    (Status <> 'InTransit' OR PaymentStatus = 'Confirmed') AND
    (Status <> 'Delivered' OR PaymentStatus IN ('Confirmed','Refunded'))),
  CONSTRAINT CK_Orders_PurchasedWhenPaid CHECK (PaymentStatus NOT IN ('Confirmed','Refunded') OR PurchasedAt IS NOT NULL),
  CONSTRAINT CK_Orders_DeliveredAt CHECK ((Status = 'Delivered') = (DeliveredAt IS NOT NULL)),
  CONSTRAINT CK_Orders_CancelledAt CHECK ((Status = 'Cancelled') = (CancelledAt IS NOT NULL)),
  CONSTRAINT CK_Orders_Money CHECK (Subtotal >= 0 AND DiscountTotal >= 0 AND ShippingTotal >= 0 AND DiscountTotal <= Subtotal),
  CONSTRAINT CK_Orders_GrandTotal CHECK (GrandTotal = Subtotal - DiscountTotal + ShippingTotal),
  CONSTRAINT CK_Orders_KeyVersion CHECK (DataKeyVersion > 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- [CORE 3, 5, 11, 16] Order lines. Product name, paid price and cost are snapshots.
-- UserId is copied here ONLY so composite FKs can prove "same order, same user, same product"
-- for deliveries, ratings, comments and returns.
-- Counters (all changed under the order-line row lock, CHECKed against each other):
--   DeliveryAllocatedQuantity <= Quantity, DeliveredQuantity <= DeliveryAllocatedQuantity,
--   ReturnRequestedQuantity <= DeliveredQuantity, RefundedQuantity <= ReturnRequestedQuantity.
-- Paid price = ROUND(list price x (1 - discount rate), 2) is enforced by a CHECK.
CREATE TABLE OrderItems (
  Id          BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  OrderId     BIGINT UNSIGNED NOT NULL,
  UserId      BIGINT UNSIGNED NOT NULL,
  ProductId   BIGINT UNSIGNED NOT NULL,
  ProductName VARCHAR(200) NOT NULL,
  Sku         VARCHAR(64) NOT NULL,
  Quantity    INT UNSIGNED NOT NULL,
  ListUnitPrice DECIMAL(18,2) NOT NULL,
  PaidUnitPrice DECIMAL(18,2) NOT NULL,
  UnitCost    DECIMAL(18,2) NOT NULL,
  DiscountId  BIGINT UNSIGNED NULL,
  DiscountRate DECIMAL(5,4) NULL,
  LineTotal   DECIMAL(18,2) NOT NULL,
  DeliveryAllocatedQuantity INT UNSIGNED NOT NULL DEFAULT 0,
  DeliveredQuantity        INT UNSIGNED NOT NULL DEFAULT 0,
  ReturnRequestedQuantity  INT UNSIGNED NOT NULL DEFAULT 0,
  RefundedQuantity         INT UNSIGNED NOT NULL DEFAULT 0,
  RowVersion  DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
  PRIMARY KEY (Id),
  UNIQUE KEY UX_OrderItems_OrderProduct (OrderId, ProductId),
  UNIQUE KEY UX_OrderItems_IdOrder (Id, OrderId),
  UNIQUE KEY UX_OrderItems_IdUserProduct (Id, UserId, ProductId),
  KEY IX_OrderItems_ProductSales (ProductId, OrderId),
  KEY IX_OrderItems_Discount (DiscountId),
  CONSTRAINT FK_OrderItems_Orders FOREIGN KEY (OrderId, UserId) REFERENCES Orders (Id, UserId) ON DELETE RESTRICT,
  CONSTRAINT FK_OrderItems_Products FOREIGN KEY (ProductId) REFERENCES Products (Id) ON DELETE RESTRICT,
  CONSTRAINT FK_OrderItems_Discounts FOREIGN KEY (DiscountId) REFERENCES Discounts (Id) ON DELETE RESTRICT,
  CONSTRAINT CK_OrderItems_Quantity CHECK (Quantity > 0),
  CONSTRAINT CK_OrderItems_Money CHECK (ListUnitPrice >= 0 AND PaidUnitPrice >= 0 AND UnitCost >= 0 AND PaidUnitPrice <= ListUnitPrice),
  CONSTRAINT CK_OrderItems_Discount CHECK (
    (DiscountId IS NULL AND DiscountRate IS NULL AND PaidUnitPrice = ListUnitPrice) OR
    (DiscountId IS NOT NULL AND DiscountRate IS NOT NULL AND DiscountRate > 0 AND DiscountRate <= 1
       AND PaidUnitPrice = ROUND(ListUnitPrice * (1 - DiscountRate), 2))),
  CONSTRAINT CK_OrderItems_LineTotal CHECK (LineTotal = PaidUnitPrice * Quantity),
  CONSTRAINT CK_OrderItems_Counters CHECK (
    DeliveryAllocatedQuantity <= Quantity AND DeliveredQuantity <= DeliveryAllocatedQuantity AND
    ReturnRequestedQuantity <= DeliveredQuantity AND RefundedQuantity <= ReturnRequestedQuantity)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- [CORE 4, 15] Payment attempts (mock bank). Never stores card number, CVV or expiry.
-- An attempt belongs to a cart of the same user; OrderId is set only when it is confirmed,
-- so failed attempts survive the rolled-back checkout. UNIQUE(UserId, key hash) is the
-- owner-scoped idempotency key.
CREATE TABLE Payments (
  Id                 BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  UserId             BIGINT UNSIGNED NOT NULL,
  CartId             BIGINT UNSIGNED NOT NULL,
  OrderId            BIGINT UNSIGNED NULL,
  IdempotencyKeyHash BINARY(32) NOT NULL,
  ProviderReference  VARCHAR(64) NULL,
  Status             VARCHAR(20) COLLATE utf8mb4_bin NOT NULL DEFAULT 'Pending',
  Amount             DECIMAL(18,2) NOT NULL,
  CurrencyCode       CHAR(3) COLLATE utf8mb4_bin NOT NULL DEFAULT 'TRY',
  CardBrand          VARCHAR(20) COLLATE utf8mb4_bin NOT NULL,
  CardLast4          CHAR(4) NOT NULL,
  FailureCode        VARCHAR(50) NULL,
  CreatedAt          DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  ConfirmedAt        DATETIME(6) NULL,
  PRIMARY KEY (Id),
  UNIQUE KEY UX_Payments_UserKey (UserId, IdempotencyKeyHash),
  UNIQUE KEY UX_Payments_Provider (ProviderReference),
  UNIQUE KEY UX_Payments_IdOrder (Id, OrderId),
  KEY IX_Payments_Order (OrderId),
  KEY IX_Payments_Cart (CartId, UserId),
  CONSTRAINT FK_Payments_Carts FOREIGN KEY (CartId, UserId) REFERENCES Carts (Id, UserId) ON DELETE RESTRICT,
  CONSTRAINT FK_Payments_Orders FOREIGN KEY (OrderId, UserId) REFERENCES Orders (Id, UserId) ON DELETE RESTRICT,
  CONSTRAINT CK_Payments_Status CHECK (Status IN ('Pending','Confirmed','Failed')),
  CONSTRAINT CK_Payments_Amount CHECK (Amount >= 0),
  CONSTRAINT CK_Payments_Currency CHECK (CurrencyCode = 'TRY'),
  CONSTRAINT CK_Payments_Card CHECK (CardBrand IN ('Visa','Mastercard','Amex','Troy','Other') AND CardLast4 REGEXP '^[0-9]{4}$'),
  CONSTRAINT CK_Payments_Confirmed CHECK ((Status = 'Confirmed') = (OrderId IS NOT NULL AND ConfirmedAt IS NOT NULL))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- [CORE 16] Return request for part of an order line.
-- Why: PDF 16 - delivered items, 30 days from PURCHASE, partial quantity, SalesManager
-- confirms receipt before the refund. The 30-day window compares to Orders.PurchasedAt in
-- the service (a CHECK cannot read another table). Quantity is reserved on the order line
-- (ReturnRequestedQuantity), which blocks over-returning under concurrency.
CREATE TABLE ReturnRequests (
  Id               BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  OrderItemId      BIGINT UNSIGNED NOT NULL,
  UserId           BIGINT UNSIGNED NOT NULL,
  ProductId        BIGINT UNSIGNED NOT NULL,
  Quantity         INT UNSIGNED NOT NULL,
  Reason           VARCHAR(500) NOT NULL,
  Status           VARCHAR(20) COLLATE utf8mb4_bin NOT NULL DEFAULT 'Requested',
  RequestedAt      DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  ReviewedByUserId BIGINT UNSIGNED NULL,
  ReviewedAt       DATETIME(6) NULL,
  ReviewNote       VARCHAR(500) NULL,
  ReceivedAt       DATETIME(6) NULL,
  RowVersion       DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
  PRIMARY KEY (Id),
  KEY IX_ReturnRequests_Item (OrderItemId, UserId, ProductId),
  KEY IX_ReturnRequests_UserHistory (UserId, RequestedAt),
  KEY IX_ReturnRequests_Queue (Status, RequestedAt),
  KEY IX_ReturnRequests_Reviewer (ReviewedByUserId),
  CONSTRAINT FK_ReturnRequests_OrderItems FOREIGN KEY (OrderItemId, UserId, ProductId)
    REFERENCES OrderItems (Id, UserId, ProductId) ON DELETE RESTRICT,
  CONSTRAINT FK_ReturnRequests_Reviewer FOREIGN KEY (ReviewedByUserId) REFERENCES Users (Id) ON DELETE RESTRICT,
  CONSTRAINT CK_ReturnRequests_Status CHECK (Status IN ('Requested','Received','Approved','Rejected')),
  CONSTRAINT CK_ReturnRequests_Quantity CHECK (Quantity > 0),
  CONSTRAINT CK_ReturnRequests_Review CHECK (
    (Status = 'Requested' AND ReviewedByUserId IS NULL AND ReviewedAt IS NULL AND ReceivedAt IS NULL) OR
    (Status = 'Rejected'  AND ReviewedByUserId IS NOT NULL AND ReviewedAt IS NOT NULL AND ReceivedAt IS NULL) OR
    (Status IN ('Received','Approved') AND ReviewedByUserId IS NOT NULL AND ReviewedAt IS NOT NULL AND ReceivedAt IS NOT NULL))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- [CORE 16] Refund records. Exactly one refund per return request (unique), idempotent by key.
-- Reason 'Cancellation' covers refunding a paid order cancelled in Processing (one per order).
-- PaymentId must belong to the same order (composite FK).
CREATE TABLE Refunds (
  Id                 BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  OrderId            BIGINT UNSIGNED NOT NULL,
  PaymentId          BIGINT UNSIGNED NOT NULL,
  ReturnRequestId    BIGINT UNSIGNED NULL,
  Reason             VARCHAR(20) COLLATE utf8mb4_bin NOT NULL,
  IdempotencyKeyHash BINARY(32) NOT NULL,
  Amount             DECIMAL(18,2) NOT NULL,
  CurrencyCode       CHAR(3) COLLATE utf8mb4_bin NOT NULL DEFAULT 'TRY',
  Status             VARCHAR(20) COLLATE utf8mb4_bin NOT NULL DEFAULT 'Pending',
  CreatedAt          DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  CompletedAt        DATETIME(6) NULL,
  CancelOrderId      BIGINT UNSIGNED GENERATED ALWAYS AS (IF(Reason = 'Cancellation', OrderId, NULL)) VIRTUAL,
  PRIMARY KEY (Id),
  UNIQUE KEY UX_Refunds_Key (IdempotencyKeyHash),
  UNIQUE KEY UX_Refunds_ReturnRequest (ReturnRequestId),
  UNIQUE KEY UX_Refunds_OneCancellation (CancelOrderId),
  KEY IX_Refunds_Order (OrderId),
  KEY IX_Refunds_Payment (PaymentId, OrderId),
  KEY IX_Refunds_Completed (CompletedAt),
  CONSTRAINT FK_Refunds_Orders FOREIGN KEY (OrderId) REFERENCES Orders (Id) ON DELETE RESTRICT,
  CONSTRAINT FK_Refunds_Payments FOREIGN KEY (PaymentId, OrderId) REFERENCES Payments (Id, OrderId) ON DELETE RESTRICT,
  CONSTRAINT FK_Refunds_ReturnRequests FOREIGN KEY (ReturnRequestId) REFERENCES ReturnRequests (Id) ON DELETE RESTRICT,
  CONSTRAINT CK_Refunds_Reason CHECK (Reason IN ('Return','Cancellation') AND ((Reason = 'Return') = (ReturnRequestId IS NOT NULL))),
  CONSTRAINT CK_Refunds_Status CHECK (Status IN ('Pending','Completed','Failed')),
  CONSTRAINT CK_Refunds_Amount CHECK (Amount > 0),
  CONSTRAINT CK_Refunds_Currency CHECK (CurrencyCode = 'TRY'),
  CONSTRAINT CK_Refunds_Completed CHECK ((Status = 'Completed') = (CompletedAt IS NOT NULL))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;


-- ---------------------------------------------------------------------
-- 6. INVOICES AND E-MAIL OUTBOX
-- ---------------------------------------------------------------------

-- [CORE 4, 11, 17] One invoice per paid order. Totals and customer data are snapshots; the
-- lines are the order's immutable OrderItems (not copied again). The PDF itself is stored
-- outside the database: only its storage key and SHA-256 are kept.
CREATE TABLE Invoices (
  Id                BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  OrderId           BIGINT UNSIGNED NOT NULL,
  InvoiceNumber     VARCHAR(24) NOT NULL,
  CustomerName      VARCHAR(100) NOT NULL,
  CustomerTaxIdEnc  VARBINARY(256) NOT NULL,
  BillingAddressEnc VARBINARY(2048) NOT NULL,
  DataKeyVersion    SMALLINT UNSIGNED NOT NULL,
  CurrencyCode      CHAR(3) COLLATE utf8mb4_bin NOT NULL DEFAULT 'TRY',
  Subtotal          DECIMAL(18,2) NOT NULL,
  DiscountTotal     DECIMAL(18,2) NOT NULL,
  ShippingTotal     DECIMAL(18,2) NOT NULL,
  GrandTotal        DECIMAL(18,2) NOT NULL,
  PdfStorageKey     VARCHAR(255) NOT NULL,
  PdfSha256         BINARY(32) NOT NULL,
  IssuedAt          DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  PRIMARY KEY (Id),
  UNIQUE KEY UX_Invoices_Order (OrderId),
  UNIQUE KEY UX_Invoices_Number (InvoiceNumber),
  KEY IX_Invoices_IssuedAt (IssuedAt),
  CONSTRAINT FK_Invoices_Orders FOREIGN KEY (OrderId) REFERENCES Orders (Id) ON DELETE RESTRICT,
  CONSTRAINT CK_Invoices_Currency CHECK (CurrencyCode = 'TRY'),
  CONSTRAINT CK_Invoices_Money CHECK (Subtotal >= 0 AND DiscountTotal >= 0 AND ShippingTotal >= 0 AND GrandTotal = Subtotal - DiscountTotal + ShippingTotal),
  CONSTRAINT CK_Invoices_KeyVersion CHECK (DataKeyVersion > 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- [CORE 4, 16] E-mail outbox (invoice PDF mail, refund notice). Why: sending mail must be
-- retryable and must never block checkout or duplicate an invoice. Workers claim a row with a
-- lease (LeaseOwner/LeaseExpiresAt). DedupKey makes enqueueing idempotent.
-- Each kind points at its own source row through its own nullable FK (no polymorphic key).
CREATE TABLE EmailOutbox (
  Id                BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  DedupKey          VARCHAR(120) NOT NULL,
  Kind              VARCHAR(20) COLLATE utf8mb4_bin NOT NULL,
  RecipientUserId   BIGINT UNSIGNED NOT NULL,
  RecipientEmailEnc VARBINARY(512) NOT NULL,
  DataKeyVersion    SMALLINT UNSIGNED NOT NULL,
  InvoiceId         BIGINT UNSIGNED NULL,
  RefundId          BIGINT UNSIGNED NULL,
  Status            VARCHAR(20) COLLATE utf8mb4_bin NOT NULL DEFAULT 'Pending',
  AttemptCount      SMALLINT UNSIGNED NOT NULL DEFAULT 0,
  NextAttemptAt     DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  LeaseOwner        VARCHAR(64) NULL,
  LeaseExpiresAt    DATETIME(6) NULL,
  LastError         VARCHAR(500) NULL,
  CreatedAt         DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  SentAt            DATETIME(6) NULL,
  PRIMARY KEY (Id),
  UNIQUE KEY UX_EmailOutbox_Dedup (DedupKey),
  KEY IX_EmailOutbox_Dequeue (Status, NextAttemptAt),
  KEY IX_EmailOutbox_Recipient (RecipientUserId),
  KEY IX_EmailOutbox_Invoice (InvoiceId),
  KEY IX_EmailOutbox_Refund (RefundId),
  CONSTRAINT FK_EmailOutbox_Users FOREIGN KEY (RecipientUserId) REFERENCES Users (Id) ON DELETE RESTRICT,
  CONSTRAINT FK_EmailOutbox_Invoices FOREIGN KEY (InvoiceId) REFERENCES Invoices (Id) ON DELETE RESTRICT,
  CONSTRAINT FK_EmailOutbox_Refunds FOREIGN KEY (RefundId) REFERENCES Refunds (Id) ON DELETE RESTRICT,
  CONSTRAINT CK_EmailOutbox_Kind CHECK (
    (Kind = 'InvoiceEmail' AND InvoiceId IS NOT NULL AND RefundId IS NULL) OR
    (Kind = 'RefundNotice' AND RefundId IS NOT NULL AND InvoiceId IS NULL)),
  CONSTRAINT CK_EmailOutbox_Status CHECK (Status IN ('Pending','Sending','Sent','Failed')),
  CONSTRAINT CK_EmailOutbox_Sent CHECK ((Status = 'Sent') = (SentAt IS NOT NULL)),
  CONSTRAINT CK_EmailOutbox_KeyVersion CHECK (DataKeyVersion > 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;


-- ---------------------------------------------------------------------
-- 7. DELIVERY
-- ---------------------------------------------------------------------

-- [CORE 3, 12] Delivery. Why: PDF 12 delivery list (delivery ID, customer ID, address,
-- completed flag) managed by the ProductManager. UserId is tied to the order's owner by the
-- composite FK. IsCompleted is a computed flag, never edited by hand.
CREATE TABLE Deliveries (
  Id                 BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  OrderId            BIGINT UNSIGNED NOT NULL,
  UserId             BIGINT UNSIGNED NOT NULL,
  Status             VARCHAR(20) COLLATE utf8mb4_bin NOT NULL DEFAULT 'Processing',
  ShippingAddressEnc VARBINARY(2048) NOT NULL,
  DataKeyVersion     SMALLINT UNSIGNED NOT NULL,
  CreatedAt          DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  CompletedAt        DATETIME(6) NULL,
  RowVersion         DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
  IsCompleted        TINYINT(1) GENERATED ALWAYS AS (Status = 'Delivered') VIRTUAL,
  PRIMARY KEY (Id),
  UNIQUE KEY UX_Deliveries_IdOrder (Id, OrderId),
  KEY IX_Deliveries_Queue (Status, Id),
  KEY IX_Deliveries_Order (OrderId, UserId),
  CONSTRAINT FK_Deliveries_Orders FOREIGN KEY (OrderId, UserId) REFERENCES Orders (Id, UserId) ON DELETE RESTRICT,
  CONSTRAINT CK_Deliveries_Status CHECK (Status IN ('Processing','InTransit','Delivered','Cancelled')),
  CONSTRAINT CK_Deliveries_Completed CHECK ((Status = 'Delivered') = (CompletedAt IS NOT NULL)),
  CONSTRAINT CK_Deliveries_KeyVersion CHECK (DataKeyVersion > 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- [CORE 12] Delivery lines (product, quantity, total price). A line can only point at an
-- order line of the SAME order as its delivery (two composite FKs share OrderId).
CREATE TABLE DeliveryItems (
  DeliveryId         BIGINT UNSIGNED NOT NULL,
  OrderItemId        BIGINT UNSIGNED NOT NULL,
  OrderId            BIGINT UNSIGNED NOT NULL,
  Quantity           INT UNSIGNED NOT NULL,
  TotalPriceSnapshot DECIMAL(18,2) NOT NULL,
  PRIMARY KEY (DeliveryId, OrderItemId),
  KEY IX_DeliveryItems_OrderItem (OrderItemId, OrderId),
  KEY IX_DeliveryItems_Delivery (DeliveryId, OrderId),
  CONSTRAINT FK_DeliveryItems_Deliveries FOREIGN KEY (DeliveryId, OrderId) REFERENCES Deliveries (Id, OrderId) ON DELETE RESTRICT,
  CONSTRAINT FK_DeliveryItems_OrderItems FOREIGN KEY (OrderItemId, OrderId) REFERENCES OrderItems (Id, OrderId) ON DELETE RESTRICT,
  CONSTRAINT CK_DeliveryItems_Quantity CHECK (Quantity > 0),
  CONSTRAINT CK_DeliveryItems_Price CHECK (TotalPriceSnapshot >= 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;


-- ---------------------------------------------------------------------
-- 8. RATINGS AND COMMENTS
-- ---------------------------------------------------------------------

-- [CORE 5] Rating 1-5, visible at once, one per user and product.
-- The composite FK proves the order line belongs to the same user and product.
-- "The line was delivered" (DeliveredQuantity > 0) is checked by the service.
CREATE TABLE Ratings (
  Id          BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  UserId      BIGINT UNSIGNED NOT NULL,
  ProductId   BIGINT UNSIGNED NOT NULL,
  OrderItemId BIGINT UNSIGNED NOT NULL,
  Stars       TINYINT UNSIGNED NOT NULL,
  CreatedAt   DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  UpdatedAt   DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  PRIMARY KEY (Id),
  UNIQUE KEY UX_Ratings_UserProduct (UserId, ProductId),
  KEY IX_Ratings_Product (ProductId),
  KEY IX_Ratings_OrderItem (OrderItemId, UserId, ProductId),
  CONSTRAINT FK_Ratings_OrderItems FOREIGN KEY (OrderItemId, UserId, ProductId)
    REFERENCES OrderItems (Id, UserId, ProductId) ON DELETE RESTRICT,
  CONSTRAINT CK_Ratings_Stars CHECK (Stars BETWEEN 1 AND 5)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- [CORE 5, 12] Comment, starts Pending, approved or rejected by a ProductManager.
-- A pending comment never hides a rating (separate tables). The moderator being a
-- ProductManager is a service rule.
CREATE TABLE Comments (
  Id                BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  UserId            BIGINT UNSIGNED NOT NULL,
  ProductId         BIGINT UNSIGNED NOT NULL,
  OrderItemId       BIGINT UNSIGNED NOT NULL,
  Body              VARCHAR(2000) NOT NULL,
  Status            VARCHAR(20) COLLATE utf8mb4_bin NOT NULL DEFAULT 'Pending',
  ModeratedByUserId BIGINT UNSIGNED NULL,
  ModeratedAt       DATETIME(6) NULL,
  ModerationNote    VARCHAR(500) NULL,
  CreatedAt         DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  UpdatedAt         DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  PRIMARY KEY (Id),
  UNIQUE KEY UX_Comments_UserProduct (UserId, ProductId),
  KEY IX_Comments_ProductVisible (ProductId, Status, CreatedAt),
  KEY IX_Comments_Queue (Status, CreatedAt),
  KEY IX_Comments_OrderItem (OrderItemId, UserId, ProductId),
  KEY IX_Comments_Moderator (ModeratedByUserId),
  CONSTRAINT FK_Comments_OrderItems FOREIGN KEY (OrderItemId, UserId, ProductId)
    REFERENCES OrderItems (Id, UserId, ProductId) ON DELETE RESTRICT,
  CONSTRAINT FK_Comments_Moderator FOREIGN KEY (ModeratedByUserId) REFERENCES Users (Id) ON DELETE RESTRICT,
  CONSTRAINT CK_Comments_Status CHECK (Status IN ('Pending','Approved','Rejected')),
  CONSTRAINT CK_Comments_Body CHECK (CHAR_LENGTH(TRIM(Body)) > 0),
  CONSTRAINT CK_Comments_Moderation CHECK ((Status = 'Pending') = (ModeratedByUserId IS NULL AND ModeratedAt IS NULL))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;


-- ---------------------------------------------------------------------
-- 9. SUPPORT CHAT AND NOTIFICATIONS
-- ---------------------------------------------------------------------

-- [CORE 13] Support conversation of a guest (hashed token) or a logged-in customer.
-- Profile/cart/order/wishlist context is resolved from CustomerUserId on the server, so no
-- client-supplied ID is trusted and no CartId is stored. Claim is an atomic conditional
-- UPDATE ... WHERE Status = 'Waiting'; exactly one agent can win.
CREATE TABLE Conversations (
  Id               BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  PublicId         CHAR(36) NOT NULL,
  CustomerUserId   BIGINT UNSIGNED NULL,
  GuestTokenHash   CHAR(64) NULL,
  Status           VARCHAR(20) COLLATE utf8mb4_bin NOT NULL DEFAULT 'Waiting',
  ClaimedByUserId  BIGINT UNSIGNED NULL,
  ClaimedAt        DATETIME(6) NULL,
  ClosedAt         DATETIME(6) NULL,
  CreatedAt        DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  RowVersion       DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
  PRIMARY KEY (Id),
  UNIQUE KEY UX_Conversations_PublicId (PublicId),
  KEY IX_Conversations_Queue (Status, CreatedAt),
  KEY IX_Conversations_Customer (CustomerUserId, CreatedAt),
  KEY IX_Conversations_Guest (GuestTokenHash),
  KEY IX_Conversations_Agent (ClaimedByUserId, Status),
  CONSTRAINT FK_Conversations_Customer FOREIGN KEY (CustomerUserId) REFERENCES Users (Id) ON DELETE RESTRICT,
  CONSTRAINT FK_Conversations_Agent FOREIGN KEY (ClaimedByUserId) REFERENCES Users (Id) ON DELETE RESTRICT,
  CONSTRAINT CK_Conversations_Status CHECK (Status IN ('Waiting','Active','Closed')),
  CONSTRAINT CK_Conversations_Owner CHECK ((CustomerUserId IS NOT NULL AND GuestTokenHash IS NULL) OR (CustomerUserId IS NULL AND GuestTokenHash IS NOT NULL)),
  CONSTRAINT CK_Conversations_Claim CHECK (
    (Status = 'Waiting' AND ClaimedByUserId IS NULL AND ClaimedAt IS NULL) OR
    (Status = 'Active'  AND ClaimedByUserId IS NOT NULL AND ClaimedAt IS NOT NULL) OR
    (Status = 'Closed'  AND ClosedAt IS NOT NULL))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- [CORE 13] Chat messages (owned by the conversation, so they cascade with it).
-- ReadAt = when the other side read it. A message may be attachment-only (Body NULL).
CREATE TABLE ChatMessages (
  Id             BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  ConversationId BIGINT UNSIGNED NOT NULL,
  SenderKind     VARCHAR(10) COLLATE utf8mb4_bin NOT NULL,
  SenderUserId   BIGINT UNSIGNED NULL,
  Body           TEXT NULL,
  SentAt         DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  ReadAt         DATETIME(6) NULL,
  PRIMARY KEY (Id),
  KEY IX_ChatMessages_Conversation (ConversationId, Id),
  KEY IX_ChatMessages_Sender (SenderUserId),
  CONSTRAINT FK_ChatMessages_Conversations FOREIGN KEY (ConversationId) REFERENCES Conversations (Id) ON DELETE CASCADE,
  CONSTRAINT FK_ChatMessages_Users FOREIGN KEY (SenderUserId) REFERENCES Users (Id) ON DELETE RESTRICT,
  CONSTRAINT CK_ChatMessages_Sender CHECK (
    SenderKind IN ('Customer','Guest','Agent','System') AND
    ((SenderKind IN ('Customer','Agent')) = (SenderUserId IS NOT NULL)))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- [CORE 13] Attachment metadata (PDF / image / video). Files live outside the database.
CREATE TABLE ChatAttachments (
  Id               BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  ChatMessageId    BIGINT UNSIGNED NOT NULL,
  StorageKey       VARCHAR(512) NOT NULL,
  OriginalFileName VARCHAR(255) NOT NULL,
  ContentType      VARCHAR(100) NOT NULL,
  SizeBytes        BIGINT UNSIGNED NOT NULL,
  Sha256           BINARY(32) NOT NULL,
  CreatedAt        DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  PRIMARY KEY (Id),
  KEY IX_ChatAttachments_Message (ChatMessageId),
  CONSTRAINT FK_ChatAttachments_Messages FOREIGN KEY (ChatMessageId) REFERENCES ChatMessages (Id) ON DELETE CASCADE,
  CONSTRAINT CK_ChatAttachments_Size CHECK (SizeBytes BETWEEN 1 AND 104857600),
  CONSTRAINT CK_ChatAttachments_Type CHECK (ContentType = 'application/pdf' OR ContentType LIKE 'image/%' OR ContentType LIKE 'video/%')
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- [CORE 11, 16] In-app notifications (wishlist discount, refund approved, return rejected).
-- DedupKey (for example 'disc:{DiscountId}:user:{UserId}:product:{ProductId}') makes the
-- same event impossible to notify twice.
CREATE TABLE Notifications (
  Id        BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  UserId    BIGINT UNSIGNED NOT NULL,
  Kind      VARCHAR(20) COLLATE utf8mb4_bin NOT NULL,
  ProductId BIGINT UNSIGNED NULL,
  DiscountId BIGINT UNSIGNED NULL,
  Title     VARCHAR(200) NOT NULL,
  Body      VARCHAR(1000) NOT NULL,
  DedupKey  VARCHAR(160) NOT NULL,
  ReadAt    DATETIME(6) NULL,
  CreatedAt DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  PRIMARY KEY (Id),
  UNIQUE KEY UX_Notifications_Dedup (DedupKey),
  KEY IX_Notifications_Inbox (UserId, ReadAt, CreatedAt),
  KEY IX_Notifications_Product (ProductId),
  KEY IX_Notifications_Discount (DiscountId),
  CONSTRAINT FK_Notifications_Users FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE CASCADE,
  CONSTRAINT FK_Notifications_Products FOREIGN KEY (ProductId) REFERENCES Products (Id) ON DELETE RESTRICT,
  CONSTRAINT FK_Notifications_Discounts FOREIGN KEY (DiscountId) REFERENCES Discounts (Id) ON DELETE RESTRICT,
  CONSTRAINT CK_Notifications_Kind CHECK (Kind IN ('WishlistDiscount','RefundApproved','ReturnRejected')),
  CONSTRAINT CK_Notifications_Discount CHECK (Kind <> 'WishlistDiscount' OR (ProductId IS NOT NULL AND DiscountId IS NOT NULL))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;


-- ---------------------------------------------------------------------
-- 10. VIEW
-- ---------------------------------------------------------------------

-- PDF 12 delivery list: delivery ID, customer ID, product ID, quantity, total price,
-- address (still encrypted: decrypt in the application), completed flag.
CREATE VIEW vw_DeliveryList AS
SELECT d.Id AS DeliveryId, d.UserId AS CustomerId, oi.ProductId, di.Quantity,
       di.TotalPriceSnapshot AS TotalPrice, d.ShippingAddressEnc, d.DataKeyVersion,
       d.IsCompleted, d.Status, d.OrderId
FROM Deliveries d
JOIN DeliveryItems di ON di.DeliveryId = d.Id AND di.OrderId = d.OrderId
JOIN OrderItems oi ON oi.Id = di.OrderItemId AND oi.OrderId = di.OrderId;

-- ---------------------------------------------------------------------
-- Privilege design (documentation, not executed here)
--   app account : SELECT, INSERT, UPDATE on all tables; DELETE only on CartItems, Carts,
--                 WishlistItems, Addresses, Notifications, ChatMessages, ChatAttachments,
--                 ProductImages. No DELETE on history tables (orders, payments, invoices, ...).
--   migration account : DDL privileges, used only during deployments.
-- ---------------------------------------------------------------------
