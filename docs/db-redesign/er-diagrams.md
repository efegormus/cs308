# ER diagrams (29 tables)

Source of truth: `schema.sql`. Tags: **CORE** = required by a PDF requirement, **EXT** = catalog tables
added for the "more diverse electronics" feedback. Mermaid renders on GitHub.
`||--o{` = one-to-many, `|o--o{` = optional one-to-many, `||--o|` = one-to-zero-or-one.

## 0. Overview

```mermaid
erDiagram
    Users ||--o{ Addresses : has
    Users ||--o{ Carts : owns
    Users ||--o{ WishlistItems : saves
    Users ||--o{ Orders : places
    Users ||--o{ Conversations : starts
    Users ||--o{ Notifications : receives
    Categories ||--o{ Categories : parent_of
    Categories ||--o{ Products : contains
    Brands |o--o{ Products : makes
    Distributors ||--o{ Products : supplies
    Products ||--o{ CartItems : added_as
    Products ||--o{ OrderItems : sold_as
    Products ||--o{ WishlistItems : wished
    Products ||--o{ ProductImages : shown_by
    Products ||--o{ ProductAttributeValues : described_by
    Carts ||--o{ CartItems : contains
    Orders ||--|{ OrderItems : contains
    Orders ||--o{ Payments : paid_by
    Orders ||--o| Invoices : invoiced_as
    Orders ||--o{ Deliveries : shipped_in
    Deliveries ||--|{ DeliveryItems : lists
    OrderItems ||--o{ DeliveryItems : fulfilled_by
    OrderItems ||--o{ ReturnRequests : returned_via
    ReturnRequests ||--o| Refunds : refunded_as
    OrderItems ||--o{ Ratings : rated_in
    OrderItems ||--o{ Comments : reviewed_in
    Discounts ||--|{ DiscountProducts : covers
    Products ||--o{ DiscountProducts : discounted_by
    Conversations ||--o{ ChatMessages : contains
    ChatMessages ||--o{ ChatAttachments : carries
```

## 1. Identity (Users, Addresses)

```mermaid
erDiagram
    Users ||--o{ Addresses : has
    Users {
        bigint Id PK
        char36 PublicId UK
        varchar Name
        varbinary EmailEnc "AES-GCM"
        binary32 EmailBlindIndex UK "HMAC-SHA256"
        varbinary TaxIdEnc "customers only"
        varchar PasswordHash
        varchar Role "Customer SalesManager ProductManager SupportAgent"
        char32 SecurityStamp
        smallint DataKeyVersion
    }
    Addresses {
        bigint Id PK
        bigint UserId FK
        varchar Label
        varbinary AddressEnc "AES-GCM"
        bool IsDefault "one default per user"
    }
```

## 2. Catalog (Categories, Brands, Distributors, Products, specs, images)

```mermaid
erDiagram
    Categories ||--o{ Categories : parent
    Categories ||--o{ Products : contains
    Categories ||--o{ AttributeDefinitions : defines
    Brands |o--o{ Products : makes
    Distributors ||--o{ Products : supplies
    Products ||--o{ ProductAttributeValues : has
    AttributeDefinitions ||--o{ ProductAttributeValues : typed_by
    Products ||--o{ ProductImages : has
    Categories {
        bigint Id PK
        bigint ParentId FK "null for root"
        varchar Name
        varchar Slug UK
    }
    Products {
        bigint Id PK
        bigint CategoryId FK
        bigint BrandId FK "null allowed"
        bigint DistributorId FK
        varchar Sku UK
        varchar Name
        varchar Model
        varchar SerialNumber
        text Description
        varchar WarrantyStatus
        decimal Price
        decimal Cost "null means 50 percent of paid price"
        int StockQuantity "unsigned"
        bool IsActive
        text SearchText "stored copy for FULLTEXT, i and dotless i unified"
    }
    Brands {
        bigint Id PK
        varchar Name UK
    }
    Distributors {
        bigint Id PK
        varchar Name UK
        varchar ContactInfo
    }
    AttributeDefinitions {
        bigint Id PK
        bigint CategoryId FK
        varchar Code "unique per category"
        varchar DataType "Text Number Bool"
        varchar Unit
    }
    ProductAttributeValues {
        bigint ProductId PK
        bigint AttributeDefinitionId PK
        varchar DataType FK "must match the definition"
        varchar ValueText
        decimal ValueNumber
        bool ValueBool
    }
    ProductImages {
        bigint Id PK
        bigint ProductId FK
        varchar StorageKey
        bool IsPrimary "one per product"
    }
```

## 3. Discounts, cart and wishlist

```mermaid
erDiagram
    Users |o--o{ Carts : owns
    Carts ||--o{ CartItems : contains
    Products ||--o{ CartItems : selected
    Users ||--o{ WishlistItems : saves
    Products ||--o{ WishlistItems : wished
    Users ||--o{ Discounts : creates
    Discounts ||--|{ DiscountProducts : covers
    Products ||--o{ DiscountProducts : discounted
    Carts {
        bigint Id PK
        bigint UserId FK "or GuestTokenHash, exactly one"
        char64 GuestTokenHash UK
        varchar State "Open CheckedOut Abandoned"
    }
    CartItems {
        bigint Id PK
        bigint CartId FK
        bigint ProductId FK
        int Quantity "greater than 0"
    }
    WishlistItems {
        bigint UserId PK
        bigint ProductId PK
    }
    Discounts {
        bigint Id PK
        decimal Rate "0 less than Rate up to 1"
        datetime StartsAt
        datetime EndsAt "after StartsAt"
        bigint CreatedByUserId FK
    }
    DiscountProducts {
        bigint DiscountId PK
        bigint ProductId PK
    }
```

## 4. Orders, payments, returns and refunds

```mermaid
erDiagram
    Users ||--o{ Orders : places
    Carts |o--o| Orders : source_of
    Orders ||--|{ OrderItems : contains
    Products ||--o{ OrderItems : sold_as
    Discounts |o--o{ OrderItems : applied_to
    Carts ||--o{ Payments : attempted_for
    Orders |o--o{ Payments : confirmed_by
    OrderItems ||--o{ ReturnRequests : returned_via
    ReturnRequests ||--o| Refunds : refunded_as
    Payments ||--o{ Refunds : reverses
    Orders ||--o{ Refunds : refunded_in
    Orders {
        bigint Id PK
        varchar OrderNumber UK
        bigint UserId FK
        bigint SourceCartId FK "unique"
        varchar Status "Processing InTransit Delivered Cancelled"
        varchar PaymentStatus "Pending Confirmed Failed Refunded"
        varbinary CustomerEmailEnc "snapshot"
        varbinary ShippingAddressEnc "snapshot"
        decimal GrandTotal "Subtotal minus DiscountTotal plus ShippingTotal"
        datetime PurchasedAt
    }
    OrderItems {
        bigint Id PK
        bigint OrderId FK
        bigint UserId FK "same owner as order"
        bigint ProductId FK
        varchar ProductName "snapshot"
        decimal PaidUnitPrice "snapshot"
        decimal UnitCost "snapshot"
        int Quantity
        int DeliveredQuantity "counter, at most Quantity"
        int ReturnRequestedQuantity "counter"
        int RefundedQuantity "counter"
    }
    Payments {
        bigint Id PK
        bigint UserId FK
        bigint CartId FK
        bigint OrderId FK "set when confirmed"
        binary32 IdempotencyKeyHash "unique per user"
        varchar Status "Pending Confirmed Failed"
        varchar CardBrand "brand and last 4 only"
        char4 CardLast4
    }
    ReturnRequests {
        bigint Id PK
        bigint OrderItemId FK
        int Quantity
        varchar Status "Requested Received Approved Rejected"
        bigint ReviewedByUserId FK "SalesManager"
    }
    Refunds {
        bigint Id PK
        bigint PaymentId FK
        bigint ReturnRequestId FK "unique, null for cancellation"
        binary32 IdempotencyKeyHash UK
        decimal Amount
        varchar Status "Pending Completed Failed"
    }
```

## 5. Invoices, e-mail outbox and delivery

```mermaid
erDiagram
    Orders ||--o| Invoices : invoiced_as
    Invoices ||--o{ EmailOutbox : mailed_by
    Refunds ||--o{ EmailOutbox : notified_by
    Orders ||--o{ Deliveries : shipped_in
    Deliveries ||--|{ DeliveryItems : lists
    OrderItems ||--o{ DeliveryItems : fulfilled_by
    Invoices {
        bigint Id PK
        bigint OrderId FK "unique"
        varchar InvoiceNumber UK
        decimal GrandTotal "snapshot"
        varchar PdfStorageKey "file stays outside the database"
        binary32 PdfSha256
    }
    EmailOutbox {
        bigint Id PK
        varchar DedupKey UK
        varchar Kind "InvoiceEmail RefundNotice"
        bigint InvoiceId FK "set for InvoiceEmail"
        bigint RefundId FK "set for RefundNotice"
        varchar Status "Pending Sending Sent Failed"
        int AttemptCount
        datetime LeaseExpiresAt
    }
    Deliveries {
        bigint Id PK
        bigint OrderId FK
        bigint UserId FK "customer ID of the delivery list"
        varchar Status "Processing InTransit Delivered Cancelled"
        varbinary ShippingAddressEnc
        bool IsCompleted "computed"
    }
    DeliveryItems {
        bigint DeliveryId PK
        bigint OrderItemId PK
        bigint OrderId FK "same order as delivery and line"
        int Quantity
        decimal TotalPriceSnapshot
    }
```

## 6. Ratings, comments, support chat and notifications

```mermaid
erDiagram
    OrderItems ||--o{ Ratings : proves_purchase
    OrderItems ||--o{ Comments : proves_purchase
    Users |o--o{ Comments : moderates
    Users |o--o{ Conversations : customer
    Users |o--o{ Conversations : agent_claim
    Conversations ||--o{ ChatMessages : contains
    ChatMessages ||--o{ ChatAttachments : carries
    Users ||--o{ Notifications : receives
    Ratings {
        bigint Id PK
        bigint UserId FK
        bigint ProductId FK
        bigint OrderItemId FK "same user and product"
        tinyint Stars "1 to 5"
    }
    Comments {
        bigint Id PK
        bigint UserId FK
        bigint ProductId FK
        bigint OrderItemId FK "same user and product"
        varchar Status "Pending Approved Rejected"
        bigint ModeratedByUserId FK "ProductManager"
    }
    Conversations {
        bigint Id PK
        char36 PublicId UK
        bigint CustomerUserId FK "or GuestTokenHash, exactly one"
        varchar Status "Waiting Active Closed"
        bigint ClaimedByUserId FK "set when Active"
    }
    ChatMessages {
        bigint Id PK
        bigint ConversationId FK
        varchar SenderKind "Customer Guest Agent System"
        text Body "null when attachment only"
    }
    ChatAttachments {
        bigint Id PK
        bigint ChatMessageId FK
        varchar StorageKey "file stays outside the database"
        varchar ContentType "pdf image video"
    }
    Notifications {
        bigint Id PK
        bigint UserId FK
        varchar Kind "WishlistDiscount RefundApproved ReturnRejected"
        varchar DedupKey UK "same event never notified twice"
    }
```
