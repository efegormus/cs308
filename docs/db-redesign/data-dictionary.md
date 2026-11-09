# Data dictionary (29 tables)

Generated from `schema.sql` (types, nullability, defaults and constraints are copied from the DDL).
The "Why" line of each table is the sentence kept in the SQL comment, so reviewers can see
which PDF requirement a table serves. Columns that are self-explanatory have no note.

Legend: **PK** primary key, **UK** unique key, **FK** foreign key, **CK** check constraint.
`virtual` columns are computed by MySQL. **Sensitive** columns are encrypted or hashed:
the database never holds a plain password, card number, CVV, key or raw guest token.
All DATETIME(6) values are UTC. All FKs are `ON DELETE RESTRICT` unless a table says CASCADE
(owned drafts only).

## Users  [CORE 10, 14, 17]

**Why:** One account table for customers and staff (four fixed roles). Why: PDF 10 fixes the four roles; PDF 14 lists the customer fields; PDF 17 requires protected personal data. Email is encrypted; lookups use EmailBlindIndex (HMAC-SHA256).

| Column | Type | Null | Default | Keys | Note |
|---|---|---|---|---|---|
| Id | BIGINT UNSIGNED | NOT NULL |  | PK |  |
| PublicId | CHAR(36) | NOT NULL |  | UK | Unguessable public identifier (UUID) used in URLs and tokens; Id is never exposed. |
| Name | VARCHAR(100) | NOT NULL |  |  |  |
| EmailEnc | VARBINARY(512) | NOT NULL |  |  | E-mail, encrypted in the application (AES-GCM). Sensitive. |
| EmailBlindIndex | BINARY(32) | NOT NULL |  | UK | HMAC-SHA256 of the normalised e-mail; the only way to look an account up by e-mail. Sensitive. |
| TaxIdEnc | VARBINARY(256) | NULL |  |  | Tax ID, encrypted. Required for customers, null for staff. Sensitive. |
| DataKeyVersion | SMALLINT UNSIGNED | NOT NULL |  |  | Which application key encrypted this row (allows key rotation). |
| PasswordHash | VARCHAR(255) | NOT NULL |  |  | Adaptive salted hash (never the password). Sensitive. |
| Role | VARCHAR(20) (bin) | NOT NULL | 'Customer' |  | One of the four fixed roles of PDF 10. |
| SecurityStamp | CHAR(32) | NOT NULL |  |  | Random value rotated on password/role change and logout; revokes old sessions. |
| IsActive | TINYINT(1) | NOT NULL | 1 |  |  |
| FailedLoginCount | SMALLINT UNSIGNED | NOT NULL | 0 |  |  |
| LockoutUntil | DATETIME(6) | NULL |  |  | Login blocked until this UTC time after too many failures. |
| CreatedAt | DATETIME(6) | NOT NULL | (UTC_TIMESTAMP(6)) |  |  |
| UpdatedAt | DATETIME(6) | NOT NULL | (UTC_TIMESTAMP(6)) |  |  |
| RowVersion | DATETIME(6) | NOT NULL | auto (CURRENT_TIMESTAMP(6), updated on change) |  | Optimistic-concurrency token, updated by MySQL on every change. |

**Keys:** PK (Id); UK UX_Users_PublicId (PublicId); UK UX_Users_EmailBlindIndex (EmailBlindIndex)

**Checks:**

- `CK_Users_Role`: `Role IN ('Customer','SalesManager','ProductManager','SupportAgent')`
- `CK_Users_Name`: `CHAR_LENGTH(TRIM(Name)) > 0`
- `CK_Users_KeyVersion`: `DataKeyVersion > 0`
- `CK_Users_CustomerTaxId`: `Role <> 'Customer' OR TaxIdEnc IS NOT NULL`

## Addresses  [CORE 14]

**Why:** Several delivery addresses per customer (home address = the default one). Why: PDF 14 needs a home address; multiple addresses are cheap and let checkout pick one. At most one default address per user is enforced by the unique virtual column.

| Column | Type | Null | Default | Keys | Note |
|---|---|---|---|---|---|
| Id | BIGINT UNSIGNED | NOT NULL |  | PK |  |
| UserId | BIGINT UNSIGNED | NOT NULL |  | FK |  |
| Label | VARCHAR(50) | NOT NULL |  |  |  |
| AddressEnc | VARBINARY(2048) | NOT NULL |  |  | Delivery/home address, encrypted. Sensitive. |
| DataKeyVersion | SMALLINT UNSIGNED | NOT NULL |  |  |  |
| IsDefault | TINYINT(1) | NOT NULL | 0 |  | At most one active default address per user (unique virtual column). |
| IsActive | TINYINT(1) | NOT NULL | 1 |  |  |
| CreatedAt | DATETIME(6) | NOT NULL | (UTC_TIMESTAMP(6)) |  |  |
| UpdatedAt | DATETIME(6) | NOT NULL | (UTC_TIMESTAMP(6)) |  |  |
| DefaultOwnerId | BIGINT UNSIGNED |  | virtual | UK |  |

**Keys:** PK (Id); UK UX_Addresses_OneDefault (DefaultOwnerId)

**Foreign keys:**

- `FK_Addresses_Users`: (UserId) -> Users (Id), ON DELETE CASCADE

**Checks:**

- `CK_Addresses_KeyVersion`: `DataKeyVersion > 0`

**Other indexes:** IX_Addresses_User (UserId)

## Brands  [EXT kept]

**Why:** Brands. Why: filter and display for a broader electronics catalog.

| Column | Type | Null | Default | Keys | Note |
|---|---|---|---|---|---|
| Id | BIGINT UNSIGNED | NOT NULL |  | PK |  |
| Name | VARCHAR(100) | NOT NULL |  | UK |  |
| IsActive | TINYINT(1) | NOT NULL | 1 |  |  |
| CreatedAt | DATETIME(6) | NOT NULL | (UTC_TIMESTAMP(6)) |  |  |

**Keys:** PK (Id); UK UX_Brands_Name (Name)

## Categories  [CORE 1, 12]

**Why:** Category tree (adjacency list, depth limit is a service rule). Why: PDF 1 needs categories; depth is 2-3 for electronics, so ParentId is enough. ParentKey makes (parent, name) unique even for root categories (NULL parent). A category being its own parent / cycles are prevented by the service (a CHECK cannot read the AUTO_INCREMENT Id in MySQL).

| Column | Type | Null | Default | Keys | Note |
|---|---|---|---|---|---|
| Id | BIGINT UNSIGNED | NOT NULL |  | PK |  |
| ParentId | BIGINT UNSIGNED | NULL |  | FK | Parent category; null for a root category (adjacency list). |
| Name | VARCHAR(100) | NOT NULL |  |  |  |
| Slug | VARCHAR(120) | NOT NULL |  | UK |  |
| Description | TEXT | NULL |  |  |  |
| SortOrder | SMALLINT UNSIGNED | NOT NULL | 0 |  |  |
| IsActive | TINYINT(1) | NOT NULL | 1 |  |  |
| CreatedAt | DATETIME(6) | NOT NULL | (UTC_TIMESTAMP(6)) |  |  |
| ParentKey | BIGINT UNSIGNED |  | virtual |  | Virtual: ParentId or 0, so (parent, name) is unique even for roots. |

**Keys:** PK (Id); UK UX_Categories_Slug (Slug); UK UX_Categories_ParentName (ParentKey, Name)

**Foreign keys:**

- `FK_Categories_Parent`: (ParentId) -> Categories (Id), ON DELETE RESTRICT

## Distributors  [CORE 9]

**Why:** Distributors. Why: PDF 9 requires distributor information on every product; a table avoids repeating contact text on each product.

| Column | Type | Null | Default | Keys | Note |
|---|---|---|---|---|---|
| Id | BIGINT UNSIGNED | NOT NULL |  | PK |  |
| Name | VARCHAR(150) | NOT NULL |  | UK |  |
| ContactInfo | VARCHAR(500) | NOT NULL |  |  | Distributor information required by PDF 9. |
| IsActive | TINYINT(1) | NOT NULL | 1 |  |  |
| CreatedAt | DATETIME(6) | NOT NULL | (UTC_TIMESTAMP(6)) |  |  |

**Keys:** PK (Id); UK UX_Distributors_Name (Name)

## Products  [CORE 1, 3, 7, 9, 11]

**Why:** Product = the sellable item (one row per sellable thing). Why: PDF 9 lists the product fields (ID, name, model, serial number, description, stock, price, warranty, distributor); PDF 3 needs stock per product; PDF 11 needs price and cost. A 256 GB and a 512 GB phone are two products (Trendyol style), each with its own price/stock. Cost NULL means "50% of the paid price" (PDF 11); checkout snapshots the result. StockQuantity is UNSIGNED: a decrement below zero fails in STRICT mode (last-unit race). RowVersion is the optimistic-concurrency token for stock and price edits. Search (PDF 7): FULLTEXT on SearchText, a STORED computed copy of name + model + description in which Turkish dotless/dotted i ('ı', 'İ') is replaced by 'i'. The default collation already ignores case and accents (Şarj = sarj) but treats 'ı' and 'i' as different letters, so the application must apply the same replacement to the user's search text. Tokens shorter than 3 characters (for example the model 'X1') are not indexed: use a LIKE fallback for those. Stock is NOT part of the search, so out-of-stock products stay searchable.

| Column | Type | Null | Default | Keys | Note |
|---|---|---|---|---|---|
| Id | BIGINT UNSIGNED | NOT NULL |  | PK |  |
| CategoryId | BIGINT UNSIGNED | NOT NULL |  | FK |  |
| BrandId | BIGINT UNSIGNED | NULL |  | FK |  |
| DistributorId | BIGINT UNSIGNED | NOT NULL |  | FK |  |
| Sku | VARCHAR(64) | NOT NULL |  | UK | Stock-keeping code, unique per product. |
| Name | VARCHAR(200) | NOT NULL |  |  |  |
| Model | VARCHAR(100) | NOT NULL |  |  |  |
| SerialNumber | VARCHAR(100) | NOT NULL |  |  | Serial number required by PDF 9 (product-level, not per physical unit). |
| Description | TEXT | NOT NULL |  |  |  |
| WarrantyStatus | VARCHAR(100) | NOT NULL |  |  | Warranty status required by PDF 9 (e.g. '2 years manufacturer'). |
| Price | DECIMAL(18,2) | NOT NULL |  |  |  |
| Cost | DECIMAL(18,2) | NULL |  |  | Unit cost. Null means the default of 50% of the paid price (PDF 11). |
| StockQuantity | INT UNSIGNED | NOT NULL | 0 |  | Units in stock. Unsigned: cannot go below zero. |
| IsActive | TINYINT(1) | NOT NULL | 1 |  | Soft delete / unpublish; sold products are never deleted. |
| CreatedAt | DATETIME(6) | NOT NULL | (UTC_TIMESTAMP(6)) |  |  |
| UpdatedAt | DATETIME(6) | NOT NULL | (UTC_TIMESTAMP(6)) |  |  |
| RowVersion | DATETIME(6) | NOT NULL | auto (CURRENT_TIMESTAMP(6), updated on change) |  | Optimistic-concurrency token for price and stock edits. |
| SearchText | TEXT |  | stored |  | Stored copy of name + model + description with 'ı'/'İ' replaced by 'i'; the only FULLTEXT-indexed column. Apply the same replacement to the search text. |

**Keys:** PK (Id); UK UX_Products_Sku (Sku)

**Foreign keys:**

- `FK_Products_Categories`: (CategoryId) -> Categories (Id), ON DELETE RESTRICT
- `FK_Products_Brands`: (BrandId) -> Brands (Id), ON DELETE RESTRICT
- `FK_Products_Distributors`: (DistributorId) -> Distributors (Id), ON DELETE RESTRICT

**Checks:**

- `CK_Products_Price`: `Price >= 0`
- `CK_Products_Cost`: `Cost IS NULL OR Cost >= 0`
- `CK_Products_ActiveDetails`: `IsActive = 0 OR ( CHAR_LENGTH(TRIM(Sku)) > 0 AND CHAR_LENGTH(TRIM(Name)) > 0 AND CHAR_LENGTH(TRIM(Model)) > 0 AND CHAR_LENGTH(TRIM(SerialNumber)) > 0 AND CHAR_LENGTH(TRIM(Description)) > 0 AND CHAR_LENGTH(TRIM(WarrantyStatus)) > 0)`

**Other indexes:** IX_Products_CategoryList (CategoryId, IsActive, Name); IX_Products_PriceSort (IsActive, Price); IX_Products_Brand (BrandId); IX_Products_Distributor (DistributorId); FT_Products_Search (FULLTEXT) (SearchText)

## AttributeDefinitions  [EXT kept]

**Why:** Attribute definitions per category (typed technical specs). Why: monitors, headphones and phones have different specs (resolution, connection, RAM). Instead of one JSON blob, each spec has a declared type, so values are validated and filterable. A definition belongs to a category and applies to its whole subtree (the subtree lookup is a service/query rule).

| Column | Type | Null | Default | Keys | Note |
|---|---|---|---|---|---|
| Id | BIGINT UNSIGNED | NOT NULL |  | PK |  |
| CategoryId | BIGINT UNSIGNED | NOT NULL |  | FK |  |
| Code | VARCHAR(50) | NOT NULL |  |  | Machine name of the spec (e.g. 'resolution'), unique per category. |
| Label | VARCHAR(100) | NOT NULL |  |  |  |
| DataType | VARCHAR(10) (bin) | NOT NULL |  |  | Text, Number or Bool; the value table must match it. |
| Unit | VARCHAR(20) | NULL |  |  |  |
| IsFilterable | TINYINT(1) | NOT NULL | 0 |  | Whether the storefront may offer this spec as a filter. |
| SortOrder | SMALLINT UNSIGNED | NOT NULL | 0 |  |  |

**Keys:** PK (Id); UK UX_AttributeDefinitions_Code (CategoryId, Code); UK UX_AttributeDefinitions_Typed (Id, DataType)

**Foreign keys:**

- `FK_AttributeDefinitions_Categories`: (CategoryId) -> Categories (Id), ON DELETE RESTRICT

**Checks:**

- `CK_AttributeDefinitions_DataType`: `DataType IN ('Text','Number','Bool')`

## ProductAttributeValues  [EXT kept]

**Why:** Spec values of a product. Exactly one typed value column is filled, and the composite FK forces it to match the DataType declared by the definition.

| Column | Type | Null | Default | Keys | Note |
|---|---|---|---|---|---|
| ProductId | BIGINT UNSIGNED | NOT NULL |  | PK, FK |  |
| AttributeDefinitionId | BIGINT UNSIGNED | NOT NULL |  | PK, FK |  |
| DataType | VARCHAR(10) (bin) | NOT NULL |  | FK | Copy of the definition's type, forced equal by a composite FK. |
| ValueText | VARCHAR(255) | NULL |  |  |  |
| ValueNumber | DECIMAL(18,4) | NULL |  |  |  |
| ValueBool | TINYINT(1) | NULL |  |  |  |

**Keys:** PK (ProductId, AttributeDefinitionId)

**Foreign keys:**

- `FK_ProductAttributeValues_Products`: (ProductId) -> Products (Id), ON DELETE RESTRICT
- `FK_ProductAttributeValues_Definitions`: (AttributeDefinitionId, DataType) -> AttributeDefinitions (Id, DataType), ON DELETE RESTRICT

**Checks:**

- `CK_ProductAttributeValues_Typed`: `(DataType = 'Text' AND ValueText IS NOT NULL AND ValueNumber IS NULL AND ValueBool IS NULL) OR (DataType = 'Number' AND ValueNumber IS NOT NULL AND ValueText IS NULL AND ValueBool IS NULL) OR (DataType = 'Bool' AND ValueBool IS NOT NULL AND ValueText IS NULL AND ValueNumber IS NULL)`

**Other indexes:** IX_ProductAttributeValues_Number (AttributeDefinitionId, ValueNumber); IX_ProductAttributeValues_Text (AttributeDefinitionId, ValueText)

## ProductImages  [EXT kept]

**Why:** Product images. Why: a catalog with many product types needs more than one picture per product. Files live in object storage; only the key is stored. One primary image per product (unique virtual column, NULL for non-primary rows).

| Column | Type | Null | Default | Keys | Note |
|---|---|---|---|---|---|
| Id | BIGINT UNSIGNED | NOT NULL |  | PK |  |
| ProductId | BIGINT UNSIGNED | NOT NULL |  | FK |  |
| StorageKey | VARCHAR(512) | NOT NULL |  |  | Object-storage key or URL; the image file is not in the database. |
| AltText | VARCHAR(200) | NULL |  |  |  |
| SortOrder | SMALLINT UNSIGNED | NOT NULL | 0 |  |  |
| IsPrimary | TINYINT(1) | NOT NULL | 0 |  |  |
| CreatedAt | DATETIME(6) | NOT NULL | (UTC_TIMESTAMP(6)) |  |  |
| PrimaryProductId | BIGINT UNSIGNED |  | virtual | UK | Virtual: ProductId for the primary image only, so there is one primary per product. |

**Keys:** PK (Id); UK UX_ProductImages_OnePrimary (PrimaryProductId)

**Foreign keys:**

- `FK_ProductImages_Products`: (ProductId) -> Products (Id), ON DELETE CASCADE

**Other indexes:** IX_ProductImages_Product (ProductId, SortOrder)

## Discounts  [CORE 11]

**Why:** Discount campaign set by a SalesManager (rate + products; system computes price). Overlapping campaigns: the highest single rate wins (computed at checkout, never stacked).

| Column | Type | Null | Default | Keys | Note |
|---|---|---|---|---|---|
| Id | BIGINT UNSIGNED | NOT NULL |  | PK |  |
| Name | VARCHAR(100) | NOT NULL |  |  |  |
| Rate | DECIMAL(5,4) | NOT NULL |  |  | Discount fraction, 0 < Rate <= 1 (0.20 = 20% off). |
| StartsAt | DATETIME(6) | NOT NULL |  |  |  |
| EndsAt | DATETIME(6) | NOT NULL |  |  |  |
| CreatedByUserId | BIGINT UNSIGNED | NOT NULL |  | FK |  |
| CancelledAt | DATETIME(6) | NULL |  |  | Set when a SalesManager ends a campaign early. |
| CreatedAt | DATETIME(6) | NOT NULL | (UTC_TIMESTAMP(6)) |  |  |

**Keys:** PK (Id)

**Foreign keys:**

- `FK_Discounts_Users`: (CreatedByUserId) -> Users (Id), ON DELETE RESTRICT

**Checks:**

- `CK_Discounts_Rate`: `Rate > 0 AND Rate <= 1`
- `CK_Discounts_Period`: `EndsAt > StartsAt`

**Other indexes:** IX_Discounts_Active (StartsAt, EndsAt); IX_Discounts_CreatedBy (CreatedByUserId)

## DiscountProducts  [CORE 11]

**Why:** Products covered by a discount.

| Column | Type | Null | Default | Keys | Note |
|---|---|---|---|---|---|
| DiscountId | BIGINT UNSIGNED | NOT NULL |  | PK, FK |  |
| ProductId | BIGINT UNSIGNED | NOT NULL |  | PK, FK |  |

**Keys:** PK (DiscountId, ProductId)

**Foreign keys:**

- `FK_DiscountProducts_Discounts`: (DiscountId) -> Discounts (Id), ON DELETE RESTRICT
- `FK_DiscountProducts_Products`: (ProductId) -> Products (Id), ON DELETE RESTRICT

**Other indexes:** IX_DiscountProducts_Product (ProductId)

## Carts  [CORE 1, 4]

**Why:** Cart owned by a user or by a hashed guest token (exactly one). OpenUserId makes "one open cart per user" a database rule (NULL when not open). A guest token maps to one cart for good (unique hash); a new guest cart gets a new token.

| Column | Type | Null | Default | Keys | Note |
|---|---|---|---|---|---|
| Id | BIGINT UNSIGNED | NOT NULL |  | PK |  |
| UserId | BIGINT UNSIGNED | NULL |  | FK |  |
| GuestTokenHash | CHAR(64) | NULL |  | UK | SHA-256 of the high-entropy guest token; the raw token lives only in the guest's cookie. |
| State | VARCHAR(20) (bin) | NOT NULL | 'Open' |  | Open, CheckedOut or Abandoned. |
| CreatedAt | DATETIME(6) | NOT NULL | (UTC_TIMESTAMP(6)) |  |  |
| UpdatedAt | DATETIME(6) | NOT NULL | (UTC_TIMESTAMP(6)) |  |  |
| RowVersion | DATETIME(6) | NOT NULL | auto (CURRENT_TIMESTAMP(6), updated on change) |  |  |
| OpenUserId | BIGINT UNSIGNED |  | virtual | UK | Virtual: UserId while the cart is open. Its unique index = one open cart per user. |

**Keys:** PK (Id); UK UX_Carts_GuestToken (GuestTokenHash); UK UX_Carts_OneOpenPerUser (OpenUserId); UK UX_Carts_IdUser (Id, UserId)

**Foreign keys:**

- `FK_Carts_Users`: (UserId) -> Users (Id), ON DELETE CASCADE

**Checks:**

- `CK_Carts_State`: `State IN ('Open','CheckedOut','Abandoned')`
- `CK_Carts_Owner`: `(UserId IS NOT NULL AND GuestTokenHash IS NULL) OR (UserId IS NULL AND GuestTokenHash IS NOT NULL)`

**Other indexes:** IX_Carts_UserState (UserId, State)

## CartItems  [CORE 1, 7]

**Why:** Cart lines. Adding to a cart never changes stock (checkout does). An out-of-stock product is blocked by the service, because a CHECK cannot read another table.

| Column | Type | Null | Default | Keys | Note |
|---|---|---|---|---|---|
| Id | BIGINT UNSIGNED | NOT NULL |  | PK |  |
| CartId | BIGINT UNSIGNED | NOT NULL |  | FK |  |
| ProductId | BIGINT UNSIGNED | NOT NULL |  | FK |  |
| Quantity | INT UNSIGNED | NOT NULL |  |  | Units requested; stock is not reserved by the cart. |
| AddedAt | DATETIME(6) | NOT NULL | (UTC_TIMESTAMP(6)) |  |  |
| UpdatedAt | DATETIME(6) | NOT NULL | (UTC_TIMESTAMP(6)) |  |  |

**Keys:** PK (Id); UK UX_CartItems_CartProduct (CartId, ProductId)

**Foreign keys:**

- `FK_CartItems_Carts`: (CartId) -> Carts (Id), ON DELETE CASCADE
- `FK_CartItems_Products`: (ProductId) -> Products (Id), ON DELETE RESTRICT

**Checks:**

- `CK_CartItems_Quantity`: `Quantity > 0`

**Other indexes:** IX_CartItems_Product (ProductId)

## WishlistItems  [CORE 11, 14]

**Why:** Wishlist. Why: the discount notification (PDF 11) targets the users whose wishlist contains the discounted product.

| Column | Type | Null | Default | Keys | Note |
|---|---|---|---|---|---|
| UserId | BIGINT UNSIGNED | NOT NULL |  | PK, FK |  |
| ProductId | BIGINT UNSIGNED | NOT NULL |  | PK, FK |  |
| AddedAt | DATETIME(6) | NOT NULL | (UTC_TIMESTAMP(6)) |  |  |

**Keys:** PK (UserId, ProductId)

**Foreign keys:**

- `FK_WishlistItems_Users`: (UserId) -> Users (Id), ON DELETE CASCADE
- `FK_WishlistItems_Products`: (ProductId) -> Products (Id), ON DELETE RESTRICT

**Other indexes:** IX_WishlistItems_Product (ProductId)

## Orders  [CORE 3, 4, 14]

**Why:** Order header with purchase-time snapshots and totals. Why: PDF 3/4 need order status and history; snapshots keep invoices and reports stable. SourceCartId is unique: the same cart cannot be bought twice with different keys. Totals are tax-inclusive: GrandTotal = Subtotal - DiscountTotal + ShippingTotal.

| Column | Type | Null | Default | Keys | Note |
|---|---|---|---|---|---|
| Id | BIGINT UNSIGNED | NOT NULL |  | PK |  |
| OrderNumber | VARCHAR(24) | NOT NULL |  | UK | Readable public order number. |
| UserId | BIGINT UNSIGNED | NOT NULL |  | FK |  |
| SourceCartId | BIGINT UNSIGNED | NULL |  | FK, UK | Cart this order was created from; unique, so one cart cannot be bought twice. |
| Status | VARCHAR(20) (bin) | NOT NULL | 'Processing' |  | Fulfilment: Processing -> InTransit -> Delivered; Cancelled only from Processing. |
| PaymentStatus | VARCHAR(20) (bin) | NOT NULL | 'Pending' |  | Summary of the payment attempts; InTransit/Delivered need Confirmed (or Refunded). |
| CurrencyCode | CHAR(3) (bin) | NOT NULL | 'TRY' |  |  |
| CustomerName | VARCHAR(100) | NOT NULL |  |  |  |
| CustomerEmailEnc | VARBINARY(512) | NOT NULL |  |  | Snapshot of the customer's e-mail, encrypted. Sensitive. |
| CustomerTaxIdEnc | VARBINARY(256) | NOT NULL |  |  | Snapshot of the tax ID, encrypted. Sensitive. |
| ShippingAddressEnc | VARBINARY(2048) | NOT NULL |  |  | Snapshot of the delivery address, encrypted. Sensitive. |
| DataKeyVersion | SMALLINT UNSIGNED | NOT NULL |  |  |  |
| Subtotal | DECIMAL(18,2) | NOT NULL |  |  |  |
| DiscountTotal | DECIMAL(18,2) | NOT NULL | 0 |  |  |
| ShippingTotal | DECIMAL(18,2) | NOT NULL | 0 |  |  |
| GrandTotal | DECIMAL(18,2) | NOT NULL |  |  | Must equal Subtotal - DiscountTotal + ShippingTotal (CHECK). |
| CreatedAt | DATETIME(6) | NOT NULL | (UTC_TIMESTAMP(6)) |  |  |
| PurchasedAt | DATETIME(6) | NULL |  |  | Time the payment was confirmed; the 30-day return window counts from here. |
| DeliveredAt | DATETIME(6) | NULL |  |  |  |
| CancelledAt | DATETIME(6) | NULL |  |  |  |
| CancelReason | VARCHAR(500) | NULL |  |  |  |
| RowVersion | DATETIME(6) | NOT NULL | auto (CURRENT_TIMESTAMP(6), updated on change) |  |  |

**Keys:** PK (Id); UK UX_Orders_Number (OrderNumber); UK UX_Orders_SourceCart (SourceCartId); UK UX_Orders_IdUser (Id, UserId)

**Foreign keys:**

- `FK_Orders_Users`: (UserId) -> Users (Id), ON DELETE RESTRICT
- `FK_Orders_Carts`: (SourceCartId) -> Carts (Id), ON DELETE RESTRICT

**Checks:**

- `CK_Orders_Status`: `Status IN ('Processing','InTransit','Delivered','Cancelled')`
- `CK_Orders_PaymentStatus`: `PaymentStatus IN ('Pending','Confirmed','Failed','Refunded')`
- `CK_Orders_Currency`: `CurrencyCode = 'TRY'`
- `CK_Orders_FulfillmentPayment`: `(Status <> 'InTransit' OR PaymentStatus = 'Confirmed') AND (Status <> 'Delivered' OR PaymentStatus IN ('Confirmed','Refunded'))`
- `CK_Orders_PurchasedWhenPaid`: `PaymentStatus NOT IN ('Confirmed','Refunded') OR PurchasedAt IS NOT NULL`
- `CK_Orders_DeliveredAt`: `(Status = 'Delivered') = (DeliveredAt IS NOT NULL)`
- `CK_Orders_CancelledAt`: `(Status = 'Cancelled') = (CancelledAt IS NOT NULL)`
- `CK_Orders_Money`: `Subtotal >= 0 AND DiscountTotal >= 0 AND ShippingTotal >= 0 AND DiscountTotal <= Subtotal`
- `CK_Orders_GrandTotal`: `GrandTotal = Subtotal - DiscountTotal + ShippingTotal`
- `CK_Orders_KeyVersion`: `DataKeyVersion > 0`

**Other indexes:** IX_Orders_UserHistory (UserId, CreatedAt); IX_Orders_PurchasedAt (PurchasedAt)

## OrderItems  [CORE 3, 5, 11, 16]

**Why:** Order lines. Product name, paid price and cost are snapshots. UserId is copied here ONLY so composite FKs can prove "same order, same user, same product" for deliveries, ratings, comments and returns. Counters (all changed under the order-line row lock, CHECKed against each other): DeliveryAllocatedQuantity <= Quantity, DeliveredQuantity <= DeliveryAllocatedQuantity, ReturnRequestedQuantity <= DeliveredQuantity, RefundedQuantity <= ReturnRequestedQuantity. Paid price = ROUND(list price x (1 - discount rate), 2) is enforced by a CHECK.

| Column | Type | Null | Default | Keys | Note |
|---|---|---|---|---|---|
| Id | BIGINT UNSIGNED | NOT NULL |  | PK |  |
| OrderId | BIGINT UNSIGNED | NOT NULL |  | FK |  |
| UserId | BIGINT UNSIGNED | NOT NULL |  | FK | Copy of the order owner, used only by composite FKs (same user for rating, return, ...). |
| ProductId | BIGINT UNSIGNED | NOT NULL |  | FK |  |
| ProductName | VARCHAR(200) | NOT NULL |  |  | Name at purchase time (snapshot). |
| Sku | VARCHAR(64) | NOT NULL |  |  |  |
| Quantity | INT UNSIGNED | NOT NULL |  |  |  |
| ListUnitPrice | DECIMAL(18,2) | NOT NULL |  |  | Price before discount at purchase time (snapshot). |
| PaidUnitPrice | DECIMAL(18,2) | NOT NULL |  |  | Price actually paid per unit; refunds use this value (PDF 16). |
| UnitCost | DECIMAL(18,2) | NOT NULL |  |  | Unit cost at purchase time (explicit cost, else 50% of PaidUnitPrice). |
| DiscountId | BIGINT UNSIGNED | NULL |  | FK |  |
| DiscountRate | DECIMAL(5,4) | NULL |  |  | Discount rate applied at purchase; PaidUnitPrice must equal ROUND(List x (1 - rate), 2). |
| LineTotal | DECIMAL(18,2) | NOT NULL |  |  |  |
| DeliveryAllocatedQuantity | INT UNSIGNED | NOT NULL | 0 |  | Units already placed on deliveries; at most Quantity. |
| DeliveredQuantity | INT UNSIGNED | NOT NULL | 0 |  | Units delivered; at most DeliveryAllocatedQuantity. |
| ReturnRequestedQuantity | INT UNSIGNED | NOT NULL | 0 |  | Units with a return request not rejected; at most DeliveredQuantity. |
| RefundedQuantity | INT UNSIGNED | NOT NULL | 0 |  | Units refunded; at most ReturnRequestedQuantity. Subtracted from popularity and revenue. |
| RowVersion | DATETIME(6) | NOT NULL | auto (CURRENT_TIMESTAMP(6), updated on change) |  |  |

**Keys:** PK (Id); UK UX_OrderItems_OrderProduct (OrderId, ProductId); UK UX_OrderItems_IdOrder (Id, OrderId); UK UX_OrderItems_IdUserProduct (Id, UserId, ProductId)

**Foreign keys:**

- `FK_OrderItems_Orders`: (OrderId, UserId) -> Orders (Id, UserId), ON DELETE RESTRICT
- `FK_OrderItems_Products`: (ProductId) -> Products (Id), ON DELETE RESTRICT
- `FK_OrderItems_Discounts`: (DiscountId) -> Discounts (Id), ON DELETE RESTRICT

**Checks:**

- `CK_OrderItems_Quantity`: `Quantity > 0`
- `CK_OrderItems_Money`: `ListUnitPrice >= 0 AND PaidUnitPrice >= 0 AND UnitCost >= 0 AND PaidUnitPrice <= ListUnitPrice`
- `CK_OrderItems_Discount`: `(DiscountId IS NULL AND DiscountRate IS NULL AND PaidUnitPrice = ListUnitPrice) OR (DiscountId IS NOT NULL AND DiscountRate IS NOT NULL AND DiscountRate > 0 AND DiscountRate <= 1 AND PaidUnitPrice = ROUND(ListUnitPrice * (1 - DiscountRate), 2))`
- `CK_OrderItems_LineTotal`: `LineTotal = PaidUnitPrice * Quantity`
- `CK_OrderItems_Counters`: `DeliveryAllocatedQuantity <= Quantity AND DeliveredQuantity <= DeliveryAllocatedQuantity AND ReturnRequestedQuantity <= DeliveredQuantity AND RefundedQuantity <= ReturnRequestedQuantity`

**Other indexes:** IX_OrderItems_ProductSales (ProductId, OrderId); IX_OrderItems_Discount (DiscountId)

## Payments  [CORE 4, 15]

**Why:** Payment attempts (mock bank). Never stores card number, CVV or expiry. An attempt belongs to a cart of the same user; OrderId is set only when it is confirmed, so failed attempts survive the rolled-back checkout. UNIQUE(UserId, key hash) is the owner-scoped idempotency key.

| Column | Type | Null | Default | Keys | Note |
|---|---|---|---|---|---|
| Id | BIGINT UNSIGNED | NOT NULL |  | PK |  |
| UserId | BIGINT UNSIGNED | NOT NULL |  | FK |  |
| CartId | BIGINT UNSIGNED | NOT NULL |  | FK | Cart being paid. Failed attempts keep this link even though no order exists. |
| OrderId | BIGINT UNSIGNED | NULL |  | FK | Set only when the payment is confirmed. |
| IdempotencyKeyHash | BINARY(32) | NOT NULL |  |  | SHA-256 of the client key; unique per user, so a retry returns the same result. |
| ProviderReference | VARCHAR(64) | NULL |  | UK |  |
| Status | VARCHAR(20) (bin) | NOT NULL | 'Pending' |  |  |
| Amount | DECIMAL(18,2) | NOT NULL |  |  |  |
| CurrencyCode | CHAR(3) (bin) | NOT NULL | 'TRY' |  |  |
| CardBrand | VARCHAR(20) (bin) | NOT NULL |  |  | Card brand only. Number, CVV and expiry are never stored (PDF 15, 17). |
| CardLast4 | CHAR(4) | NOT NULL |  |  | Last four digits only. |
| FailureCode | VARCHAR(50) | NULL |  |  |  |
| CreatedAt | DATETIME(6) | NOT NULL | (UTC_TIMESTAMP(6)) |  |  |
| ConfirmedAt | DATETIME(6) | NULL |  |  |  |

**Keys:** PK (Id); UK UX_Payments_UserKey (UserId, IdempotencyKeyHash); UK UX_Payments_Provider (ProviderReference); UK UX_Payments_IdOrder (Id, OrderId)

**Foreign keys:**

- `FK_Payments_Carts`: (CartId, UserId) -> Carts (Id, UserId), ON DELETE RESTRICT
- `FK_Payments_Orders`: (OrderId, UserId) -> Orders (Id, UserId), ON DELETE RESTRICT

**Checks:**

- `CK_Payments_Status`: `Status IN ('Pending','Confirmed','Failed')`
- `CK_Payments_Amount`: `Amount >= 0`
- `CK_Payments_Currency`: `CurrencyCode = 'TRY'`
- `CK_Payments_Card`: `CardBrand IN ('Visa','Mastercard','Amex','Troy','Other') AND CardLast4 REGEXP '^[0-9]{4}$'`
- `CK_Payments_Confirmed`: `(Status = 'Confirmed') = (OrderId IS NOT NULL AND ConfirmedAt IS NOT NULL)`

**Other indexes:** IX_Payments_Order (OrderId); IX_Payments_Cart (CartId, UserId)

## ReturnRequests  [CORE 16]

**Why:** Return request for part of an order line. Why: PDF 16 - delivered items, 30 days from PURCHASE, partial quantity, SalesManager confirms receipt before the refund. The 30-day window compares to Orders.PurchasedAt in the service (a CHECK cannot read another table). Quantity is reserved on the order line (ReturnRequestedQuantity), which blocks over-returning under concurrency.

| Column | Type | Null | Default | Keys | Note |
|---|---|---|---|---|---|
| Id | BIGINT UNSIGNED | NOT NULL |  | PK |  |
| OrderItemId | BIGINT UNSIGNED | NOT NULL |  | FK |  |
| UserId | BIGINT UNSIGNED | NOT NULL |  | FK |  |
| ProductId | BIGINT UNSIGNED | NOT NULL |  | FK | Copy of the line's product, used by the composite FK to the order line. |
| Quantity | INT UNSIGNED | NOT NULL |  |  |  |
| Reason | VARCHAR(500) | NOT NULL |  |  |  |
| Status | VARCHAR(20) (bin) | NOT NULL | 'Requested' |  | Requested -> Received (SalesManager confirms the product is back) -> Approved (refund authorised); or Rejected. |
| RequestedAt | DATETIME(6) | NOT NULL | (UTC_TIMESTAMP(6)) |  |  |
| ReviewedByUserId | BIGINT UNSIGNED | NULL |  | FK |  |
| ReviewedAt | DATETIME(6) | NULL |  |  |  |
| ReviewNote | VARCHAR(500) | NULL |  |  |  |
| ReceivedAt | DATETIME(6) | NULL |  |  | When the SalesManager confirmed receipt of the product (PDF 16). |
| RowVersion | DATETIME(6) | NOT NULL | auto (CURRENT_TIMESTAMP(6), updated on change) |  |  |

**Keys:** PK (Id)

**Foreign keys:**

- `FK_ReturnRequests_OrderItems`: (OrderItemId, UserId, ProductId) -> OrderItems (Id, UserId, ProductId), ON DELETE RESTRICT
- `FK_ReturnRequests_Reviewer`: (ReviewedByUserId) -> Users (Id), ON DELETE RESTRICT

**Checks:**

- `CK_ReturnRequests_Status`: `Status IN ('Requested','Received','Approved','Rejected')`
- `CK_ReturnRequests_Quantity`: `Quantity > 0`
- `CK_ReturnRequests_Review`: `(Status = 'Requested' AND ReviewedByUserId IS NULL AND ReviewedAt IS NULL AND ReceivedAt IS NULL) OR (Status = 'Rejected' AND ReviewedByUserId IS NOT NULL AND ReviewedAt IS NOT NULL AND ReceivedAt IS NULL) OR (Status IN ('Received','Approved') AND ReviewedByUserId IS NOT NULL AND ReviewedAt IS NOT NULL AND ReceivedAt IS NOT NULL)`

**Other indexes:** IX_ReturnRequests_Item (OrderItemId, UserId, ProductId); IX_ReturnRequests_UserHistory (UserId, RequestedAt); IX_ReturnRequests_Queue (Status, RequestedAt); IX_ReturnRequests_Reviewer (ReviewedByUserId)

## Refunds  [CORE 16]

**Why:** Refund records. Exactly one refund per return request (unique), idempotent by key. Reason 'Cancellation' covers refunding a paid order cancelled in Processing (one per order). PaymentId must belong to the same order (composite FK).

| Column | Type | Null | Default | Keys | Note |
|---|---|---|---|---|---|
| Id | BIGINT UNSIGNED | NOT NULL |  | PK |  |
| OrderId | BIGINT UNSIGNED | NOT NULL |  | FK |  |
| PaymentId | BIGINT UNSIGNED | NOT NULL |  | FK |  |
| ReturnRequestId | BIGINT UNSIGNED | NULL |  | FK, UK | Unique: one refund per return request. Null for a cancellation refund. |
| Reason | VARCHAR(20) (bin) | NOT NULL |  |  |  |
| IdempotencyKeyHash | BINARY(32) | NOT NULL |  | UK | Unique: the same refund cannot be paid twice. |
| Amount | DECIMAL(18,2) | NOT NULL |  |  |  |
| CurrencyCode | CHAR(3) (bin) | NOT NULL | 'TRY' |  |  |
| Status | VARCHAR(20) (bin) | NOT NULL | 'Pending' |  |  |
| CreatedAt | DATETIME(6) | NOT NULL | (UTC_TIMESTAMP(6)) |  |  |
| CompletedAt | DATETIME(6) | NULL |  |  |  |
| CancelOrderId | BIGINT UNSIGNED |  | virtual | UK | Virtual: OrderId for cancellation refunds, so there is one per order. |

**Keys:** PK (Id); UK UX_Refunds_Key (IdempotencyKeyHash); UK UX_Refunds_ReturnRequest (ReturnRequestId); UK UX_Refunds_OneCancellation (CancelOrderId)

**Foreign keys:**

- `FK_Refunds_Orders`: (OrderId) -> Orders (Id), ON DELETE RESTRICT
- `FK_Refunds_Payments`: (PaymentId, OrderId) -> Payments (Id, OrderId), ON DELETE RESTRICT
- `FK_Refunds_ReturnRequests`: (ReturnRequestId) -> ReturnRequests (Id), ON DELETE RESTRICT

**Checks:**

- `CK_Refunds_Reason`: `Reason IN ('Return','Cancellation') AND ((Reason = 'Return') = (ReturnRequestId IS NOT NULL))`
- `CK_Refunds_Status`: `Status IN ('Pending','Completed','Failed')`
- `CK_Refunds_Amount`: `Amount > 0`
- `CK_Refunds_Currency`: `CurrencyCode = 'TRY'`
- `CK_Refunds_Completed`: `(Status = 'Completed') = (CompletedAt IS NOT NULL)`

**Other indexes:** IX_Refunds_Order (OrderId); IX_Refunds_Payment (PaymentId, OrderId); IX_Refunds_Completed (CompletedAt)

## Invoices  [CORE 4, 11, 17]

**Why:** One invoice per paid order. Totals and customer data are snapshots; the lines are the order's immutable OrderItems (not copied again). The PDF itself is stored outside the database: only its storage key and SHA-256 are kept.

| Column | Type | Null | Default | Keys | Note |
|---|---|---|---|---|---|
| Id | BIGINT UNSIGNED | NOT NULL |  | PK |  |
| OrderId | BIGINT UNSIGNED | NOT NULL |  | FK, UK | Unique: one invoice per paid order. |
| InvoiceNumber | VARCHAR(24) | NOT NULL |  | UK | Unique readable invoice number. |
| CustomerName | VARCHAR(100) | NOT NULL |  |  |  |
| CustomerTaxIdEnc | VARBINARY(256) | NOT NULL |  |  | Tax ID snapshot, encrypted. Sensitive. |
| BillingAddressEnc | VARBINARY(2048) | NOT NULL |  |  | Address snapshot, encrypted. Sensitive. |
| DataKeyVersion | SMALLINT UNSIGNED | NOT NULL |  |  |  |
| CurrencyCode | CHAR(3) (bin) | NOT NULL | 'TRY' |  |  |
| Subtotal | DECIMAL(18,2) | NOT NULL |  |  |  |
| DiscountTotal | DECIMAL(18,2) | NOT NULL |  |  |  |
| ShippingTotal | DECIMAL(18,2) | NOT NULL |  |  |  |
| GrandTotal | DECIMAL(18,2) | NOT NULL |  |  |  |
| PdfStorageKey | VARCHAR(255) | NOT NULL |  |  | Where the PDF file is stored outside the database. |
| PdfSha256 | BINARY(32) | NOT NULL |  |  | SHA-256 of the PDF content, to detect tampering or loss. |
| IssuedAt | DATETIME(6) | NOT NULL | (UTC_TIMESTAMP(6)) |  |  |

**Keys:** PK (Id); UK UX_Invoices_Order (OrderId); UK UX_Invoices_Number (InvoiceNumber)

**Foreign keys:**

- `FK_Invoices_Orders`: (OrderId) -> Orders (Id), ON DELETE RESTRICT

**Checks:**

- `CK_Invoices_Currency`: `CurrencyCode = 'TRY'`
- `CK_Invoices_Money`: `Subtotal >= 0 AND DiscountTotal >= 0 AND ShippingTotal >= 0 AND GrandTotal = Subtotal - DiscountTotal + ShippingTotal`
- `CK_Invoices_KeyVersion`: `DataKeyVersion > 0`

**Other indexes:** IX_Invoices_IssuedAt (IssuedAt)

## EmailOutbox  [CORE 4, 16]

**Why:** E-mail outbox (invoice PDF mail, refund notice). Why: sending mail must be retryable and must never block checkout or duplicate an invoice. Workers claim a row with a lease (LeaseOwner/LeaseExpiresAt). DedupKey makes enqueueing idempotent. Each kind points at its own source row through its own nullable FK (no polymorphic key).

| Column | Type | Null | Default | Keys | Note |
|---|---|---|---|---|---|
| Id | BIGINT UNSIGNED | NOT NULL |  | PK |  |
| DedupKey | VARCHAR(120) | NOT NULL |  | UK | Unique key, e.g. 'invoice:{InvoiceId}', so a mail is enqueued only once. |
| Kind | VARCHAR(20) (bin) | NOT NULL |  |  |  |
| RecipientUserId | BIGINT UNSIGNED | NOT NULL |  | FK |  |
| RecipientEmailEnc | VARBINARY(512) | NOT NULL |  |  | Recipient address at enqueue time, encrypted. Sensitive. |
| DataKeyVersion | SMALLINT UNSIGNED | NOT NULL |  |  |  |
| InvoiceId | BIGINT UNSIGNED | NULL |  | FK |  |
| RefundId | BIGINT UNSIGNED | NULL |  | FK |  |
| Status | VARCHAR(20) (bin) | NOT NULL | 'Pending' |  |  |
| AttemptCount | SMALLINT UNSIGNED | NOT NULL | 0 |  |  |
| NextAttemptAt | DATETIME(6) | NOT NULL | (UTC_TIMESTAMP(6)) |  |  |
| LeaseOwner | VARCHAR(64) | NULL |  |  | Worker that claimed the row; with LeaseExpiresAt it prevents double sending. |
| LeaseExpiresAt | DATETIME(6) | NULL |  |  |  |
| LastError | VARCHAR(500) | NULL |  |  |  |
| CreatedAt | DATETIME(6) | NOT NULL | (UTC_TIMESTAMP(6)) |  |  |
| SentAt | DATETIME(6) | NULL |  |  |  |

**Keys:** PK (Id); UK UX_EmailOutbox_Dedup (DedupKey)

**Foreign keys:**

- `FK_EmailOutbox_Users`: (RecipientUserId) -> Users (Id), ON DELETE RESTRICT
- `FK_EmailOutbox_Invoices`: (InvoiceId) -> Invoices (Id), ON DELETE RESTRICT
- `FK_EmailOutbox_Refunds`: (RefundId) -> Refunds (Id), ON DELETE RESTRICT

**Checks:**

- `CK_EmailOutbox_Kind`: `(Kind = 'InvoiceEmail' AND InvoiceId IS NOT NULL AND RefundId IS NULL) OR (Kind = 'RefundNotice' AND RefundId IS NOT NULL AND InvoiceId IS NULL)`
- `CK_EmailOutbox_Status`: `Status IN ('Pending','Sending','Sent','Failed')`
- `CK_EmailOutbox_Sent`: `(Status = 'Sent') = (SentAt IS NOT NULL)`
- `CK_EmailOutbox_KeyVersion`: `DataKeyVersion > 0`

**Other indexes:** IX_EmailOutbox_Dequeue (Status, NextAttemptAt); IX_EmailOutbox_Recipient (RecipientUserId); IX_EmailOutbox_Invoice (InvoiceId); IX_EmailOutbox_Refund (RefundId)

## Deliveries  [CORE 3, 12]

**Why:** Delivery. Why: PDF 12 delivery list (delivery ID, customer ID, address, completed flag) managed by the ProductManager. UserId is tied to the order's owner by the composite FK. IsCompleted is a computed flag, never edited by hand.

| Column | Type | Null | Default | Keys | Note |
|---|---|---|---|---|---|
| Id | BIGINT UNSIGNED | NOT NULL |  | PK |  |
| OrderId | BIGINT UNSIGNED | NOT NULL |  | FK |  |
| UserId | BIGINT UNSIGNED | NOT NULL |  | FK | Customer ID of the delivery list (PDF 12); tied to the order owner by composite FK. |
| Status | VARCHAR(20) (bin) | NOT NULL | 'Processing' |  |  |
| ShippingAddressEnc | VARBINARY(2048) | NOT NULL |  |  | Delivery address snapshot, encrypted. Sensitive. |
| DataKeyVersion | SMALLINT UNSIGNED | NOT NULL |  |  |  |
| CreatedAt | DATETIME(6) | NOT NULL | (UTC_TIMESTAMP(6)) |  |  |
| CompletedAt | DATETIME(6) | NULL |  |  |  |
| RowVersion | DATETIME(6) | NOT NULL | auto (CURRENT_TIMESTAMP(6), updated on change) |  |  |
| IsCompleted | TINYINT(1) |  | virtual |  | Virtual: 1 when Status = 'Delivered' (the 'completed' field of PDF 12). |

**Keys:** PK (Id); UK UX_Deliveries_IdOrder (Id, OrderId)

**Foreign keys:**

- `FK_Deliveries_Orders`: (OrderId, UserId) -> Orders (Id, UserId), ON DELETE RESTRICT

**Checks:**

- `CK_Deliveries_Status`: `Status IN ('Processing','InTransit','Delivered','Cancelled')`
- `CK_Deliveries_Completed`: `(Status = 'Delivered') = (CompletedAt IS NOT NULL)`
- `CK_Deliveries_KeyVersion`: `DataKeyVersion > 0`

**Other indexes:** IX_Deliveries_Queue (Status, Id); IX_Deliveries_Order (OrderId, UserId)

## DeliveryItems  [CORE 12]

**Why:** Delivery lines (product, quantity, total price). A line can only point at an order line of the SAME order as its delivery (two composite FKs share OrderId).

| Column | Type | Null | Default | Keys | Note |
|---|---|---|---|---|---|
| DeliveryId | BIGINT UNSIGNED | NOT NULL |  | PK, FK |  |
| OrderItemId | BIGINT UNSIGNED | NOT NULL |  | PK, FK |  |
| OrderId | BIGINT UNSIGNED | NOT NULL |  | FK | Same order as both the delivery and the order line (two composite FKs). |
| Quantity | INT UNSIGNED | NOT NULL |  |  |  |
| TotalPriceSnapshot | DECIMAL(18,2) | NOT NULL |  |  | Total price of this delivery line at purchase time (PDF 12). |

**Keys:** PK (DeliveryId, OrderItemId)

**Foreign keys:**

- `FK_DeliveryItems_Deliveries`: (DeliveryId, OrderId) -> Deliveries (Id, OrderId), ON DELETE RESTRICT
- `FK_DeliveryItems_OrderItems`: (OrderItemId, OrderId) -> OrderItems (Id, OrderId), ON DELETE RESTRICT

**Checks:**

- `CK_DeliveryItems_Quantity`: `Quantity > 0`
- `CK_DeliveryItems_Price`: `TotalPriceSnapshot >= 0`

**Other indexes:** IX_DeliveryItems_OrderItem (OrderItemId, OrderId); IX_DeliveryItems_Delivery (DeliveryId, OrderId)

## Ratings  [CORE 5]

**Why:** Rating 1-5, visible at once, one per user and product. The composite FK proves the order line belongs to the same user and product. "The line was delivered" (DeliveredQuantity > 0) is checked by the service.

| Column | Type | Null | Default | Keys | Note |
|---|---|---|---|---|---|
| Id | BIGINT UNSIGNED | NOT NULL |  | PK |  |
| UserId | BIGINT UNSIGNED | NOT NULL |  | FK |  |
| ProductId | BIGINT UNSIGNED | NOT NULL |  | FK |  |
| OrderItemId | BIGINT UNSIGNED | NOT NULL |  | FK | The purchased line that proves the user bought this product. |
| Stars | TINYINT UNSIGNED | NOT NULL |  |  | 1 to 5, visible immediately (PDF 5). |
| CreatedAt | DATETIME(6) | NOT NULL | (UTC_TIMESTAMP(6)) |  |  |
| UpdatedAt | DATETIME(6) | NOT NULL | (UTC_TIMESTAMP(6)) |  |  |

**Keys:** PK (Id); UK UX_Ratings_UserProduct (UserId, ProductId)

**Foreign keys:**

- `FK_Ratings_OrderItems`: (OrderItemId, UserId, ProductId) -> OrderItems (Id, UserId, ProductId), ON DELETE RESTRICT

**Checks:**

- `CK_Ratings_Stars`: `Stars BETWEEN 1 AND 5`

**Other indexes:** IX_Ratings_Product (ProductId); IX_Ratings_OrderItem (OrderItemId, UserId, ProductId)

## Comments  [CORE 5, 12]

**Why:** Comment, starts Pending, approved or rejected by a ProductManager. A pending comment never hides a rating (separate tables). The moderator being a ProductManager is a service rule.

| Column | Type | Null | Default | Keys | Note |
|---|---|---|---|---|---|
| Id | BIGINT UNSIGNED | NOT NULL |  | PK |  |
| UserId | BIGINT UNSIGNED | NOT NULL |  | FK |  |
| ProductId | BIGINT UNSIGNED | NOT NULL |  | FK |  |
| OrderItemId | BIGINT UNSIGNED | NOT NULL |  | FK |  |
| Body | VARCHAR(2000) | NOT NULL |  |  |  |
| Status | VARCHAR(20) (bin) | NOT NULL | 'Pending' |  | Pending until a ProductManager approves or rejects (PDF 5). |
| ModeratedByUserId | BIGINT UNSIGNED | NULL |  | FK | ProductManager who decided (role checked by the service). |
| ModeratedAt | DATETIME(6) | NULL |  |  |  |
| ModerationNote | VARCHAR(500) | NULL |  |  |  |
| CreatedAt | DATETIME(6) | NOT NULL | (UTC_TIMESTAMP(6)) |  |  |
| UpdatedAt | DATETIME(6) | NOT NULL | (UTC_TIMESTAMP(6)) |  |  |

**Keys:** PK (Id); UK UX_Comments_UserProduct (UserId, ProductId)

**Foreign keys:**

- `FK_Comments_OrderItems`: (OrderItemId, UserId, ProductId) -> OrderItems (Id, UserId, ProductId), ON DELETE RESTRICT
- `FK_Comments_Moderator`: (ModeratedByUserId) -> Users (Id), ON DELETE RESTRICT

**Checks:**

- `CK_Comments_Status`: `Status IN ('Pending','Approved','Rejected')`
- `CK_Comments_Body`: `CHAR_LENGTH(TRIM(Body)) > 0`
- `CK_Comments_Moderation`: `(Status = 'Pending') = (ModeratedByUserId IS NULL AND ModeratedAt IS NULL)`

**Other indexes:** IX_Comments_ProductVisible (ProductId, Status, CreatedAt); IX_Comments_Queue (Status, CreatedAt); IX_Comments_OrderItem (OrderItemId, UserId, ProductId); IX_Comments_Moderator (ModeratedByUserId)

## Conversations  [CORE 13]

**Why:** Support conversation of a guest (hashed token) or a logged-in customer. Profile/cart/order/wishlist context is resolved from CustomerUserId on the server, so no client-supplied ID is trusted and no CartId is stored. Claim is an atomic conditional UPDATE ... WHERE Status = 'Waiting'; exactly one agent can win.

| Column | Type | Null | Default | Keys | Note |
|---|---|---|---|---|---|
| Id | BIGINT UNSIGNED | NOT NULL |  | PK |  |
| PublicId | CHAR(36) | NOT NULL |  | UK |  |
| CustomerUserId | BIGINT UNSIGNED | NULL |  | FK |  |
| GuestTokenHash | CHAR(64) | NULL |  |  | SHA-256 of the guest token for anonymous chats. |
| Status | VARCHAR(20) (bin) | NOT NULL | 'Waiting' |  |  |
| ClaimedByUserId | BIGINT UNSIGNED | NULL |  | FK | Agent who claimed the chat (atomic conditional UPDATE). |
| ClaimedAt | DATETIME(6) | NULL |  |  |  |
| ClosedAt | DATETIME(6) | NULL |  |  |  |
| CreatedAt | DATETIME(6) | NOT NULL | (UTC_TIMESTAMP(6)) |  |  |
| RowVersion | DATETIME(6) | NOT NULL | auto (CURRENT_TIMESTAMP(6), updated on change) |  |  |

**Keys:** PK (Id); UK UX_Conversations_PublicId (PublicId)

**Foreign keys:**

- `FK_Conversations_Customer`: (CustomerUserId) -> Users (Id), ON DELETE RESTRICT
- `FK_Conversations_Agent`: (ClaimedByUserId) -> Users (Id), ON DELETE RESTRICT

**Checks:**

- `CK_Conversations_Status`: `Status IN ('Waiting','Active','Closed')`
- `CK_Conversations_Owner`: `(CustomerUserId IS NOT NULL AND GuestTokenHash IS NULL) OR (CustomerUserId IS NULL AND GuestTokenHash IS NOT NULL)`
- `CK_Conversations_Claim`: `(Status = 'Waiting' AND ClaimedByUserId IS NULL AND ClaimedAt IS NULL) OR (Status = 'Active' AND ClaimedByUserId IS NOT NULL AND ClaimedAt IS NOT NULL) OR (Status = 'Closed' AND ClosedAt IS NOT NULL)`

**Other indexes:** IX_Conversations_Queue (Status, CreatedAt); IX_Conversations_Customer (CustomerUserId, CreatedAt); IX_Conversations_Guest (GuestTokenHash); IX_Conversations_Agent (ClaimedByUserId, Status)

## ChatMessages  [CORE 13]

**Why:** Chat messages (owned by the conversation, so they cascade with it). ReadAt = when the other side read it. A message may be attachment-only (Body NULL).

| Column | Type | Null | Default | Keys | Note |
|---|---|---|---|---|---|
| Id | BIGINT UNSIGNED | NOT NULL |  | PK |  |
| ConversationId | BIGINT UNSIGNED | NOT NULL |  | FK |  |
| SenderKind | VARCHAR(10) (bin) | NOT NULL |  |  | Customer, Guest, Agent or System. |
| SenderUserId | BIGINT UNSIGNED | NULL |  | FK |  |
| Body | TEXT | NULL |  |  |  |
| SentAt | DATETIME(6) | NOT NULL | (UTC_TIMESTAMP(6)) |  |  |
| ReadAt | DATETIME(6) | NULL |  |  | When the other side read the message. |

**Keys:** PK (Id)

**Foreign keys:**

- `FK_ChatMessages_Conversations`: (ConversationId) -> Conversations (Id), ON DELETE CASCADE
- `FK_ChatMessages_Users`: (SenderUserId) -> Users (Id), ON DELETE RESTRICT

**Checks:**

- `CK_ChatMessages_Sender`: `SenderKind IN ('Customer','Guest','Agent','System') AND ((SenderKind IN ('Customer','Agent')) = (SenderUserId IS NOT NULL))`

**Other indexes:** IX_ChatMessages_Conversation (ConversationId, Id); IX_ChatMessages_Sender (SenderUserId)

## ChatAttachments  [CORE 13]

**Why:** Attachment metadata (PDF / image / video). Files live outside the database.

| Column | Type | Null | Default | Keys | Note |
|---|---|---|---|---|---|
| Id | BIGINT UNSIGNED | NOT NULL |  | PK |  |
| ChatMessageId | BIGINT UNSIGNED | NOT NULL |  | FK |  |
| StorageKey | VARCHAR(512) | NOT NULL |  |  | File location outside the database. |
| OriginalFileName | VARCHAR(255) | NOT NULL |  |  |  |
| ContentType | VARCHAR(100) | NOT NULL |  |  | Only PDF, image/* or video/* (PDF 13). |
| SizeBytes | BIGINT UNSIGNED | NOT NULL |  |  |  |
| Sha256 | BINARY(32) | NOT NULL |  |  | Sensitive. |
| CreatedAt | DATETIME(6) | NOT NULL | (UTC_TIMESTAMP(6)) |  |  |

**Keys:** PK (Id)

**Foreign keys:**

- `FK_ChatAttachments_Messages`: (ChatMessageId) -> ChatMessages (Id), ON DELETE CASCADE

**Checks:**

- `CK_ChatAttachments_Size`: `SizeBytes BETWEEN 1 AND 104857600`
- `CK_ChatAttachments_Type`: `ContentType = 'application/pdf' OR ContentType LIKE 'image/%' OR ContentType LIKE 'video/%'`

**Other indexes:** IX_ChatAttachments_Message (ChatMessageId)

## Notifications  [CORE 11, 16]

**Why:** In-app notifications (wishlist discount, refund approved, return rejected). DedupKey (for example 'disc:{DiscountId}:user:{UserId}:product:{ProductId}') makes the same event impossible to notify twice.

| Column | Type | Null | Default | Keys | Note |
|---|---|---|---|---|---|
| Id | BIGINT UNSIGNED | NOT NULL |  | PK |  |
| UserId | BIGINT UNSIGNED | NOT NULL |  | FK |  |
| Kind | VARCHAR(20) (bin) | NOT NULL |  |  |  |
| ProductId | BIGINT UNSIGNED | NULL |  | FK |  |
| DiscountId | BIGINT UNSIGNED | NULL |  | FK |  |
| Title | VARCHAR(200) | NOT NULL |  |  |  |
| Body | VARCHAR(1000) | NOT NULL |  |  |  |
| DedupKey | VARCHAR(160) | NOT NULL |  | UK | Unique key of the event, so the same event never notifies a user twice. |
| ReadAt | DATETIME(6) | NULL |  |  |  |
| CreatedAt | DATETIME(6) | NOT NULL | (UTC_TIMESTAMP(6)) |  |  |

**Keys:** PK (Id); UK UX_Notifications_Dedup (DedupKey)

**Foreign keys:**

- `FK_Notifications_Users`: (UserId) -> Users (Id), ON DELETE CASCADE
- `FK_Notifications_Products`: (ProductId) -> Products (Id), ON DELETE RESTRICT
- `FK_Notifications_Discounts`: (DiscountId) -> Discounts (Id), ON DELETE RESTRICT

**Checks:**

- `CK_Notifications_Kind`: `Kind IN ('WishlistDiscount','RefundApproved','ReturnRejected')`
- `CK_Notifications_Discount`: `Kind <> 'WishlistDiscount' OR (ProductId IS NOT NULL AND DiscountId IS NOT NULL)`

**Other indexes:** IX_Notifications_Inbox (UserId, ReadAt, CreatedAt); IX_Notifications_Product (ProductId); IX_Notifications_Discount (DiscountId)

## View vw_DeliveryList

PDF 12 delivery list: delivery ID, customer ID, product ID, quantity, total price, address (still encrypted: decrypt in the application), completed flag. Read-only convenience view.
