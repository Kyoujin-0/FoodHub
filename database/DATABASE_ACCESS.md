# FoodHub database (Microsoft Access)

File: `database\foodhub.accdb` (created automatically on first run by `Core/AccessSchema.vb`; open it in Microsoft Access to see tables and relationships).

Types: COUNTER = AutoNumber, LONG = Long Integer, BYTE = Byte (0/1 for Yes/No flags), CURRENCY = Currency, DATETIME = Date/Time, TEXT(n) = Short Text, MEMO = Long Text.


## users

| Field | Type | Notes |
|---|---|---|
| id | COUNTER | Primary key |
| username | TEXT(20) | required |
| password_hash | TEXT(100) | required |
| role | TEXT(10) | required |
| status | TEXT(10) | required |
| created_at | DATETIME | required |
| avatar_path | TEXT(255) |  |

## stall_categories

| Field | Type | Notes |
|---|---|---|
| id | COUNTER | Primary key |
| name | TEXT(40) | required |

## stalls

| Field | Type | Notes |
|---|---|---|
| id | COUNTER | Primary key |
| owner_id | LONG | required, FK -> users.id |
| name | TEXT(60) | required |
| category_id | LONG | FK -> stall_categories.id |
| contact | TEXT(15) | required |
| description | TEXT(200) |  |
| logo_path | TEXT(255) |  |
| cover_path | TEXT(255) |  |
| is_open | BYTE | required |
| rent_due_date | DATETIME |  |

## banners

| Field | Type | Notes |
|---|---|---|
| id | COUNTER | Primary key |
| image_path | TEXT(255) |  |
| title | TEXT(80) |  |
| stall_id | LONG | required, FK -> stalls.id |
| sort_order | LONG | required |
| is_active | BYTE | required |

## menu_categories

| Field | Type | Notes |
|---|---|---|
| id | COUNTER | Primary key |
| stall_id | LONG | required, FK -> stalls.id |
| name | TEXT(40) | required |

## products

| Field | Type | Notes |
|---|---|---|
| id | COUNTER | Primary key |
| stall_id | LONG | required, FK -> stalls.id |
| menu_category_id | LONG | FK -> menu_categories.id |
| name | TEXT(80) | required |
| short_desc | TEXT(120) |  |
| description | MEMO |  |
| image_path | TEXT(255) |  |
| base_price | CURRENCY | required |
| is_available | BYTE | required |
| max_per_order | LONG |  |
| track_stock | BYTE | required |
| stock | LONG | required |
| low_stock_at | LONG | required |

## product_sizes

| Field | Type | Notes |
|---|---|---|
| id | COUNTER | Primary key |
| product_id | LONG | required, FK -> products.id |
| name | TEXT(30) | required |
| price | CURRENCY | required |

## stall_extras

| Field | Type | Notes |
|---|---|---|
| id | COUNTER | Primary key |
| stall_id | LONG | required, FK -> stalls.id |
| name | TEXT(40) | required |
| price | CURRENCY | required |

## product_extras

| Field | Type | Notes |
|---|---|---|
| id | COUNTER | Primary key |
| product_id | LONG | required, FK -> products.id |
| name | TEXT(40) | required |
| price | CURRENCY | required |
| stall_extra_id | LONG | FK -> stall_extras.id |

## product_removables

| Field | Type | Notes |
|---|---|---|
| id | COUNTER | Primary key |
| product_id | LONG | required, FK -> products.id |
| name | TEXT(40) | required |

## orders

| Field | Type | Notes |
|---|---|---|
| id | COUNTER | Primary key |
| customer_id | LONG | required, FK -> users.id |
| total | CURRENCY | required |
| created_at | DATETIME | required |

## order_stalls

| Field | Type | Notes |
|---|---|---|
| id | COUNTER | Primary key |
| order_id | LONG | required, FK -> orders.id |
| stall_id | LONG | required, FK -> stalls.id |
| status | TEXT(10) | required |
| subtotal | CURRENCY | required |
| decline_reason | TEXT(200) |  |
| updated_at | DATETIME | required |
| completed_at | DATETIME |  |

## order_items

| Field | Type | Notes |
|---|---|---|
| id | COUNTER | Primary key |
| order_stall_id | LONG | required, FK -> order_stalls.id |
| product_id | LONG | FK -> products.id |
| product_name | TEXT(80) | required |
| size_name | TEXT(30) |  |
| extras_text | TEXT(255) |  |
| dine_option | TEXT(10) | required |
| quantity | LONG | required |
| unit_price | CURRENCY | required |
| extras_total | CURRENCY | required |

## reviews

| Field | Type | Notes |
|---|---|---|
| id | COUNTER | Primary key |
| product_id | LONG | required, FK -> products.id |
| customer_id | LONG | required, FK -> users.id |
| order_stall_id | LONG | FK -> order_stalls.id |
| rating | BYTE | required |
| comment | TEXT(255) |  |
| created_at | DATETIME | required |
| reply | TEXT(255) |  |
| replied_at | DATETIME |  |

## cart_items

| Field | Type | Notes |
|---|---|---|
| id | COUNTER | Primary key |
| customer_id | LONG | required, FK -> users.id |
| product_id | LONG | required, FK -> products.id |
| size_id | LONG | FK -> product_sizes.id |
| dine_option | TEXT(10) | required |
| quantity | LONG | required |
| in_order | BYTE | required |
| is_saved | BYTE | required |
| is_checked | BYTE | required |

## cart_item_extras

| Field | Type | Notes |
|---|---|---|
| cart_item_id | LONG | required, FK -> cart_items.id |
| extra_id | LONG | required, FK -> product_extras.id |
| (composite key) | | CONSTRAINT pk_cart_item_extras PRIMARY KEY (cart_item_id, extra_id) |

## messages

| Field | Type | Notes |
|---|---|---|
| id | COUNTER | Primary key |
| order_stall_id | LONG | required, FK -> order_stalls.id |
| sender_id | LONG | required, FK -> users.id |
| body | MEMO |  |
| image_path | TEXT(255) |  |
| is_read | BYTE | required |
| sent_at | DATETIME | required |

## app_settings

| Field | Type | Notes |
|---|---|---|
| setting_key | TEXT(40) | Primary key |
| setting_value | TEXT(100) | required |

## rent_payments

| Field | Type | Notes |
|---|---|---|
| id | COUNTER | Primary key |
| stall_id | LONG | required, FK -> stalls.id |
| rent_month | DATETIME | required |
| amount | CURRENCY | required |
| paid_at | DATETIME | required |
| recorded_by | LONG | FK -> users.id |
