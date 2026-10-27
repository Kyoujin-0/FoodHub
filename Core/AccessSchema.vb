Imports System.Data
Imports System.Data.OleDb
Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Text

''' <summary>
''' Creates the Microsoft Access database (foodhub.accdb) and all its tables the first time the app runs.
''' Replaces database\schema.sql of the MySQL version. Access has no triggers, ENUMs, ON DELETE SET NULL or DEFAULT clauses in this DDL,
''' so: status/role lists are checked by the app, timestamps are written by the code, and child rows are removed by the code before the parent.
''' Yes/No flags are stored as Byte 0/1 (is_open, is_available, ...), prices as Currency.
''' </summary>
Public Module AccessSchema
    ''' <summary>What went wrong while creating the relationships (empty = all fine). Also written to schema_log.txt next to the database.</summary>
    Public Warnings As String = ""

    ' ---- tables, in dependency order: name -> CREATE TABLE ----
    Private Function Tables() As List(Of KeyValuePair(Of String, String))
        Dim t As New List(Of KeyValuePair(Of String, String))
        Dim add As Action(Of String, String) = Sub(n As String, ddl As String) t.Add(New KeyValuePair(Of String, String)(n, ddl))

        add("users", "CREATE TABLE users (id COUNTER PRIMARY KEY, username TEXT(20) NOT NULL, password_hash TEXT(100) NOT NULL, " &
            "[role] TEXT(10) NOT NULL, [status] TEXT(10) NOT NULL, created_at DATETIME NOT NULL, avatar_path TEXT(255))")
        add("stall_categories", "CREATE TABLE stall_categories (id COUNTER PRIMARY KEY, [name] TEXT(40) NOT NULL)")
        add("stalls", "CREATE TABLE stalls (id COUNTER PRIMARY KEY, owner_id LONG NOT NULL, [name] TEXT(60) NOT NULL, category_id LONG, " &
            "contact TEXT(15) NOT NULL, [description] TEXT(200), logo_path TEXT(255), cover_path TEXT(255), is_open BYTE NOT NULL, rent_due_date DATETIME)")
        add("banners", BannerSvc.CreateSql)
        add("menu_categories", "CREATE TABLE menu_categories (id COUNTER PRIMARY KEY, stall_id LONG NOT NULL, [name] TEXT(40) NOT NULL)")
        add("products", "CREATE TABLE products (id COUNTER PRIMARY KEY, stall_id LONG NOT NULL, menu_category_id LONG, [name] TEXT(80) NOT NULL, " &
            "short_desc TEXT(120), [description] MEMO, image_path TEXT(255), base_price CURRENCY NOT NULL, is_available BYTE NOT NULL, " &
            "max_per_order LONG, track_stock BYTE NOT NULL, stock LONG NOT NULL, low_stock_at LONG NOT NULL)")
        add("product_sizes", "CREATE TABLE product_sizes (id COUNTER PRIMARY KEY, product_id LONG NOT NULL, [name] TEXT(30) NOT NULL, price CURRENCY NOT NULL)")
        add("stall_extras", "CREATE TABLE stall_extras (id COUNTER PRIMARY KEY, stall_id LONG NOT NULL, [name] TEXT(40) NOT NULL, price CURRENCY NOT NULL)")
        add("product_extras", "CREATE TABLE product_extras (id COUNTER PRIMARY KEY, product_id LONG NOT NULL, [name] TEXT(40) NOT NULL, " &
            "price CURRENCY NOT NULL, stall_extra_id LONG)")
        add("product_removables", "CREATE TABLE product_removables (id COUNTER PRIMARY KEY, product_id LONG NOT NULL, [name] TEXT(40) NOT NULL)")
        add("orders", "CREATE TABLE orders (id COUNTER PRIMARY KEY, customer_id LONG NOT NULL, total CURRENCY NOT NULL, created_at DATETIME NOT NULL)")
        add("order_stalls", "CREATE TABLE order_stalls (id COUNTER PRIMARY KEY, order_id LONG NOT NULL, stall_id LONG NOT NULL, [status] TEXT(10) NOT NULL, " &
            "subtotal CURRENCY NOT NULL, decline_reason TEXT(200), updated_at DATETIME NOT NULL, completed_at DATETIME)")
        add("order_items", "CREATE TABLE order_items (id COUNTER PRIMARY KEY, order_stall_id LONG NOT NULL, product_id LONG, product_name TEXT(80) NOT NULL, " &
            "size_name TEXT(30), extras_text TEXT(255), dine_option TEXT(10) NOT NULL, quantity LONG NOT NULL, unit_price CURRENCY NOT NULL, extras_total CURRENCY NOT NULL)")
        add("reviews", "CREATE TABLE reviews (id COUNTER PRIMARY KEY, product_id LONG NOT NULL, customer_id LONG NOT NULL, order_stall_id LONG, " &
            "rating BYTE NOT NULL, [comment] TEXT(255), created_at DATETIME NOT NULL, reply TEXT(255), replied_at DATETIME)")
        add("cart_items", "CREATE TABLE cart_items (id COUNTER PRIMARY KEY, customer_id LONG NOT NULL, product_id LONG NOT NULL, size_id LONG, " &
            "dine_option TEXT(10) NOT NULL, quantity LONG NOT NULL, in_order BYTE NOT NULL, is_saved BYTE NOT NULL, is_checked BYTE NOT NULL)")
        add("cart_item_extras", "CREATE TABLE cart_item_extras (cart_item_id LONG NOT NULL, extra_id LONG NOT NULL, " &
            "CONSTRAINT pk_cart_item_extras PRIMARY KEY (cart_item_id, extra_id))")
        add("messages", "CREATE TABLE messages (id COUNTER PRIMARY KEY, order_stall_id LONG NOT NULL, sender_id LONG NOT NULL, body MEMO, " &
            "image_path TEXT(255), is_read BYTE NOT NULL, sent_at DATETIME NOT NULL)")
        add("app_settings", "CREATE TABLE app_settings (setting_key TEXT(40) PRIMARY KEY, setting_value TEXT(100) NOT NULL)")
        add("rent_payments", "CREATE TABLE rent_payments (id COUNTER PRIMARY KEY, stall_id LONG NOT NULL, rent_month DATETIME NOT NULL, " &
            "amount CURRENCY NOT NULL, paid_at DATETIME NOT NULL, recorded_by LONG)")
        Return t
    End Function

    ' ---- unique rules and speed-ups ----
    Private Function Indexes() As String()
        Return {
            "CREATE UNIQUE INDEX ux_users_username ON users (username)",
            "CREATE UNIQUE INDEX ux_stall_categories_name ON stall_categories ([name])",
            "CREATE UNIQUE INDEX ux_stalls_owner ON stalls (owner_id)",
            "CREATE UNIQUE INDEX ux_stalls_name ON stalls ([name])",
            "CREATE UNIQUE INDEX ux_stall_extras_name ON stall_extras (stall_id, [name])",
            "CREATE UNIQUE INDEX ux_reviews_once ON reviews (customer_id, product_id, order_stall_id)",
            "CREATE INDEX ix_rent_payments_month ON rent_payments (stall_id, rent_month)",
            "CREATE INDEX ix_messages_order ON messages (order_stall_id)",
            "CREATE INDEX ix_order_items_order ON order_items (order_stall_id)"
        }
    End Function

    ' ---- relationships (foreign keys) ----
    Private Function Relationships() As String()
        Dim r As New List(Of String)
        Dim fk As Action(Of String, String, String, String) =
            Sub(child As String, col As String, parent As String, parentCol As String)
                r.Add("ALTER TABLE " & child & " ADD CONSTRAINT fk_" & child & "_" & col & " FOREIGN KEY (" & col & ") REFERENCES " & parent & " (" & parentCol & ")")
            End Sub
        fk("stalls", "owner_id", "users", "id")
        fk("stalls", "category_id", "stall_categories", "id")
        fk("banners", "stall_id", "stalls", "id")
        fk("menu_categories", "stall_id", "stalls", "id")
        fk("products", "stall_id", "stalls", "id")
        fk("products", "menu_category_id", "menu_categories", "id")
        fk("product_sizes", "product_id", "products", "id")
        fk("stall_extras", "stall_id", "stalls", "id")
        fk("product_extras", "product_id", "products", "id")
        fk("product_extras", "stall_extra_id", "stall_extras", "id")
        fk("product_removables", "product_id", "products", "id")
        fk("orders", "customer_id", "users", "id")
        fk("order_stalls", "order_id", "orders", "id")
        fk("order_stalls", "stall_id", "stalls", "id")
        fk("order_items", "order_stall_id", "order_stalls", "id")
        fk("order_items", "product_id", "products", "id")
        fk("reviews", "product_id", "products", "id")
        fk("reviews", "customer_id", "users", "id")
        fk("reviews", "order_stall_id", "order_stalls", "id")
        fk("cart_items", "customer_id", "users", "id")
        fk("cart_items", "product_id", "products", "id")
        fk("cart_items", "size_id", "product_sizes", "id")
        fk("cart_item_extras", "cart_item_id", "cart_items", "id")
        fk("cart_item_extras", "extra_id", "product_extras", "id")
        fk("messages", "order_stall_id", "order_stalls", "id")
        fk("messages", "sender_id", "users", "id")
        fk("rent_payments", "stall_id", "stalls", "id")
        fk("rent_payments", "recorded_by", "users", "id")
        Return r.ToArray()
    End Function

    ''' <summary>Makes sure the .accdb file and every table exist. Returns False with a message the user can act on.</summary>
    Public Function EnsureDatabase(ByRef errMsg As String) As Boolean
        Try
            Dim file As String = Db.DbFile()
            Directory.CreateDirectory(Path.GetDirectoryName(file))
            If Not System.IO.File.Exists(file) Then CreateEmptyFile(file)

            Dim missing As New List(Of KeyValuePair(Of String, String))
            Using cn As OleDbConnection = Db.Open()
                For Each t As KeyValuePair(Of String, String) In Tables()
                    If Not TableExists(cn, t.Key) Then missing.Add(t)
                Next
            End Using
            If missing.Count = 0 Then Return True

            For Each t As KeyValuePair(Of String, String) In missing
                Db.Exec(t.Value)
            Next
            ' only the first time (every table was missing): indexes, relationships and the rent setting
            If missing.Count = Tables().Count Then
                Dim log As New StringBuilder()
                For Each sql As String In Indexes()
                    Try
                        Db.Exec(sql)
                    Catch ex As Exception
                        log.AppendLine(sql & vbCrLf & "   -> " & ex.Message)
                    End Try
                Next
                For Each sql As String In Relationships()
                    Try
                        Db.Exec(sql)
                    Catch ex As Exception
                        log.AppendLine(sql & vbCrLf & "   -> " & ex.Message)
                    End Try
                Next
                Warnings = log.ToString()
                If Warnings <> "" Then
                    Try
                        System.IO.File.WriteAllText(Path.Combine(Path.GetDirectoryName(file), "schema_log.txt"), Warnings)
                    Catch
                    End Try
                End If
            End If
            Dim hasRent As Integer = Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM app_settings WHERE setting_key='monthly_rent'"))
            If hasRent = 0 Then Db.Exec("INSERT INTO app_settings (setting_key, setting_value) VALUES ('monthly_rent', '5000')")
            Return True
        Catch ex As Exception
            errMsg = ex.Message
            Return False
        End Try
    End Function

    Private Function TableExists(cn As OleDbConnection, name As String) As Boolean
        Using dt As DataTable = cn.GetSchema("Tables", New String() {Nothing, Nothing, name, "TABLE"})
            Return dt.Rows.Count > 0
        End Using
    End Function

    ''' <summary>Creates an empty .accdb with ADOX (part of Windows / the Access Database Engine).</summary>
    Private Sub CreateEmptyFile(file As String)
        Dim pr As String = Db.Provider()
        If pr = "" Then
            Throw New InvalidOperationException("The Microsoft Access Database Engine is not installed (or it is the wrong 32/64-bit version for this app).")
        End If
        Dim t As Type = Type.GetTypeFromProgID("ADOX.Catalog")
        If t Is Nothing Then Throw New InvalidOperationException("ADOX is not available on this computer.")
        Dim cat As Object = Activator.CreateInstance(t)
        Try
            cat.Create("Provider=" & pr & ";Data Source=" & file & ";")
        Finally
            Marshal.FinalReleaseComObject(cat)
        End Try
    End Sub
End Module
