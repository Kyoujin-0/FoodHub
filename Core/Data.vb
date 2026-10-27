Imports System.Data
Imports System.Data.OleDb
Imports System.IO
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Windows.Forms

''' <summary>
''' Tiny Microsoft Access helper (OleDb + the ACE provider). The database is one .accdb file.
''' Code keeps writing named parameters (@name): Prepare() turns them into the positional ? that OleDb needs,
''' repeating a value when the same @name appears twice.
''' Access SQL reminders: INNER JOIN / LEFT JOIN with one pair of parentheses per extra join, TOP n instead of LIMIT,
''' IIf(cond, a, b) instead of IF/CASE/IFNULL, no GROUP_CONCAT, [name] and [description] in brackets.
''' </summary>
Public Module Db
    Private _connStr As String = ""
    Private _dbFile As String = ""

    Private ReadOnly Providers As String() = {"Microsoft.ACE.OLEDB.16.0", "Microsoft.ACE.OLEDB.12.0"}

    ''' <summary>Full path of the .accdb file. db.config holds a path (relative = next to the .exe); empty = database\foodhub.accdb of the project.</summary>
    Public Function DbFile() As String
        If _dbFile <> "" Then Return _dbFile
        Dim cfg As String = ""
        Try
            Dim p As String = Path.Combine(Application.StartupPath, "db.config")
            If File.Exists(p) Then cfg = File.ReadAllText(p).Trim()
        Catch
        End Try
        If cfg.Contains("=") Then
            ' tolerate a "Data Source=xxx" style line
            Dim m As Match = Regex.Match(cfg, "Data Source\s*=\s*([^;\r\n]+)", RegexOptions.IgnoreCase)
            cfg = If(m.Success, m.Groups(1).Value.Trim(), "")
        End If
        If cfg = "" Then
            _dbFile = DefaultFile()
        ElseIf Path.IsPathRooted(cfg) Then
            _dbFile = cfg
        Else
            _dbFile = Path.GetFullPath(Path.Combine(Application.StartupPath, cfg))
        End If
        Return _dbFile
    End Function

    ''' <summary>The folder that holds foodhub.accdb. The images folder (stall logos, profile photos, menu photos, chat photos) lives here too,
    ''' so when several PCs share one database folder they also share the photos.</summary>
    Public Function DataFolder() As String
        Return Path.GetDirectoryName(DbFile())
    End Function

    ''' <summary>project\database\foodhub.accdb when run from Visual Studio (bin\Debug\...), else next to the .exe.</summary>
    Private Function DefaultFile() As String
        Try
            Dim d As DirectoryInfo = New DirectoryInfo(Application.StartupPath)
            For i As Integer = 0 To 6
                If d Is Nothing Then Exit For
                If File.Exists(Path.Combine(d.FullName, "FoodHub.vbproj")) Then
                    Return Path.Combine(d.FullName, "database", "foodhub.accdb")
                End If
                d = d.Parent
            Next
        Catch
        End Try
        Return Path.Combine(Application.StartupPath, "foodhub.accdb")
    End Function

    ''' <summary>The newest installed ACE provider ("" when none is installed for this app's bitness).</summary>
    Public Function Provider() As String
        For Each pr As String In Providers
            Try
                If Type.GetTypeFromProgID(pr) IsNot Nothing Then Return pr
            Catch
            End Try
        Next
        Return ""
    End Function

    Public Function ConnStr() As String
        If _connStr = "" Then
            Dim pr As String = Provider()
            If pr = "" Then pr = Providers(1)
            _connStr = "Provider=" & pr & ";Data Source=" & DbFile() & ";Persist Security Info=False;"
        End If
        Return _connStr
    End Function

    Public Function Open() As OleDbConnection
        Dim cn As New OleDbConnection(ConnStr())
        cn.Open()
        Return cn
    End Function

    ''' <summary>Named parameter. Values are converted to types Access understands (Decimal = Currency, Long = Integer, Boolean = 1/0).</summary>
    Public Function P(name As String, value As Object) As OleDbParameter
        Dim prm As New OleDbParameter()
        prm.ParameterName = name
        If value Is Nothing OrElse IsDBNull(value) Then
            prm.OleDbType = OleDbType.VarWChar
            prm.Size = 1
            prm.Value = DBNull.Value
        ElseIf TypeOf value Is Decimal Then
            prm.OleDbType = OleDbType.Currency
            prm.Value = value
        ElseIf TypeOf value Is DateTime Then
            prm.OleDbType = OleDbType.Date
            prm.Value = value
        ElseIf TypeOf value Is Boolean Then
            prm.OleDbType = OleDbType.Integer
            prm.Value = If(CBool(value), 1, 0)
        ElseIf TypeOf value Is Long Then
            Dim l As Long = CLng(value)
            If l >= Integer.MinValue AndAlso l <= Integer.MaxValue Then
                prm.OleDbType = OleDbType.Integer
                prm.Value = CInt(l)
            Else
                prm.OleDbType = OleDbType.Double
                prm.Value = CDbl(l)
            End If
        ElseIf TypeOf value Is Integer OrElse TypeOf value Is Short OrElse TypeOf value Is Byte Then
            prm.OleDbType = OleDbType.Integer
            prm.Value = Convert.ToInt32(value)
        ElseIf TypeOf value Is Double OrElse TypeOf value Is Single Then
            prm.OleDbType = OleDbType.Double
            prm.Value = Convert.ToDouble(value)
        Else
            Dim s As String = Convert.ToString(value)
            If s.Length > 255 Then
                prm.OleDbType = OleDbType.LongVarWChar
            Else
                prm.OleDbType = OleDbType.VarWChar
            End If
            prm.Size = Math.Max(1, s.Length)
            prm.Value = s
        End If
        Return prm
    End Function

    ''' <summary>0 becomes SQL NULL (for optional foreign keys).</summary>
    Public Function NullIfZero(i As Integer) As Object
        If i = 0 Then Return DBNull.Value
        Return i
    End Function

    ''' <summary>Replaces every @name by ? and adds the matching parameter to the command, in order of appearance.</summary>
    Private Sub Prepare(cmd As OleDbCommand, sql As String, args() As OleDbParameter)
        Dim map As New Dictionary(Of String, OleDbParameter)(StringComparer.OrdinalIgnoreCase)
        If args IsNot Nothing Then
            For Each a As OleDbParameter In args
                map(a.ParameterName) = a
            Next
        End If
        Dim sb As New StringBuilder()
        Dim inQuote As Boolean = False
        Dim i As Integer = 0
        While i < sql.Length
            Dim ch As Char = sql(i)
            If ch = "'"c Then
                inQuote = Not inQuote
                sb.Append(ch)
                i += 1
            ElseIf ch = "@"c AndAlso Not inQuote Then
                Dim j As Integer = i + 1
                While j < sql.Length AndAlso (Char.IsLetterOrDigit(sql(j)) OrElse sql(j) = "_"c)
                    j += 1
                End While
                Dim nm As String = sql.Substring(i, j - i)
                Dim src As OleDbParameter = Nothing
                If Not map.TryGetValue(nm, src) Then Throw New InvalidOperationException("SQL parameter " & nm & " has no value.")
                Dim c As New OleDbParameter(nm, src.OleDbType)
                c.Size = src.Size
                c.Value = src.Value
                cmd.Parameters.Add(c)
                sb.Append("?")
                i = j
            Else
                sb.Append(ch)
                i += 1
            End If
        End While
        cmd.CommandText = sb.ToString()
    End Sub

    Public Function Query(sql As String, ParamArray args() As OleDbParameter) As DataTable
        Using cn As OleDbConnection = Open()
            Using cmd As New OleDbCommand()
                cmd.Connection = cn
                Prepare(cmd, sql, args)
                Dim dt As New DataTable()
                Using da As New OleDbDataAdapter(cmd)
                    da.Fill(dt)
                End Using
                Return dt
            End Using
        End Using
    End Function

    Public Function Exec(sql As String, ParamArray args() As OleDbParameter) As Integer
        Using cn As OleDbConnection = Open()
            Using cmd As New OleDbCommand()
                cmd.Connection = cn
                Prepare(cmd, sql, args)
                Return cmd.ExecuteNonQuery()
            End Using
        End Using
    End Function

    Public Function Scalar(sql As String, ParamArray args() As OleDbParameter) As Object
        Using cn As OleDbConnection = Open()
            Using cmd As New OleDbCommand()
                cmd.Connection = cn
                Prepare(cmd, sql, args)
                Return cmd.ExecuteScalar()
            End Using
        End Using
    End Function

    ''' <summary>INSERT and return the new AutoNumber id.</summary>
    Public Function Insert(sql As String, ParamArray args() As OleDbParameter) As Long
        Using cn As OleDbConnection = Open()
            Return ExecOn(cn, Nothing, sql, args)
        End Using
    End Function

    ''' <summary>Run a statement on an existing connection/transaction. For an INSERT it returns the new AutoNumber id (@@IDENTITY), otherwise 0.</summary>
    Public Function ExecOn(cn As OleDbConnection, tx As OleDbTransaction, sql As String, ParamArray args() As OleDbParameter) As Long
        Using cmd As New OleDbCommand()
            cmd.Connection = cn
            If tx IsNot Nothing Then cmd.Transaction = tx
            Prepare(cmd, sql, args)
            cmd.ExecuteNonQuery()
        End Using
        If sql.TrimStart().StartsWith("INSERT", StringComparison.OrdinalIgnoreCase) Then
            Using idCmd As New OleDbCommand("SELECT @@IDENTITY", cn)
                If tx IsNot Nothing Then idCmd.Transaction = tx
                Dim o As Object = idCmd.ExecuteScalar()
                If o Is Nothing OrElse IsDBNull(o) Then Return 0
                Return Convert.ToInt64(o)
            End Using
        End If
        Return 0
    End Function

    ''' <summary>Same as ExecOn but returns the number of rows changed (used for guarded UPDATEs inside a transaction).</summary>
    Public Function ExecCountOn(cn As OleDbConnection, tx As OleDbTransaction, sql As String, ParamArray args() As OleDbParameter) As Integer
        Using cmd As New OleDbCommand()
            cmd.Connection = cn
            If tx IsNot Nothing Then cmd.Transaction = tx
            Prepare(cmd, sql, args)
            Return cmd.ExecuteNonQuery()
        End Using
    End Function

    Public Function TestConnection(ByRef errMsg As String) As Boolean
        Try
            Using cn As OleDbConnection = Open()
            End Using
            Return True
        Catch ex As Exception
            errMsg = ex.Message
            Return False
        End Try
    End Function

    ''' <summary>Access SQL for "value, or fallback when NULL" (Access has no IFNULL/COALESCE; Nz() does not work outside Access itself).</summary>
    Public Function Nz(expr As String, fallback As String) As String
        Return "IIf((" & expr & ") IS NULL, " & fallback & ", (" & expr & "))"
    End Function

    ' ---- DataRow readers (null-safe) ----
    Public Function GetStr(r As DataRow, col As String) As String
        If IsDBNull(r(col)) Then Return ""
        Return Convert.ToString(r(col))
    End Function

    Public Function GetInt(r As DataRow, col As String) As Integer
        If IsDBNull(r(col)) Then Return 0
        Return Convert.ToInt32(r(col))
    End Function

    Public Function GetDec(r As DataRow, col As String) As Decimal
        If IsDBNull(r(col)) Then Return 0D
        Return Convert.ToDecimal(r(col))
    End Function

    Public Function GetDbl(r As DataRow, col As String) As Double
        If IsDBNull(r(col)) Then Return 0.0
        Return Convert.ToDouble(r(col))
    End Function

    Public Function GetBool(r As DataRow, col As String) As Boolean
        If IsDBNull(r(col)) Then Return False
        Return Convert.ToInt32(r(col)) <> 0
    End Function

    Public Function GetDate(r As DataRow, col As String) As DateTime
        If IsDBNull(r(col)) Then Return DateTime.MinValue
        Return Convert.ToDateTime(r(col))
    End Function

    ''' <summary>Adds a text column to a table that lists, per row, the lines of a child table joined with ", " (replaces MySQL GROUP_CONCAT).
    ''' childSql must return keyCol (the id found in dt's idCol) and textCol, already in the order wanted.</summary>
    Public Sub AttachJoinedText(dt As DataTable, idCol As String, newCol As String, childSql As String, keyCol As String, textCol As String)
        If Not dt.Columns.Contains(newCol) Then dt.Columns.Add(newCol, GetType(String))
        If dt.Rows.Count = 0 Then Return
        Dim ids As New List(Of String)
        For Each r As DataRow In dt.Rows
            ids.Add(Convert.ToInt32(r(idCol)).ToString())
        Next
        Dim kids As DataTable = Query(childSql.Replace("{IDS}", String.Join(",", ids)))
        Dim parts As New Dictionary(Of Integer, List(Of String))
        For Each k As DataRow In kids.Rows
            Dim id As Integer = Convert.ToInt32(k(keyCol))
            If Not parts.ContainsKey(id) Then parts(id) = New List(Of String)
            parts(id).Add(Convert.ToString(k(textCol)))
        Next
        For Each r As DataRow In dt.Rows
            Dim id As Integer = Convert.ToInt32(r(idCol))
            r(newCol) = If(parts.ContainsKey(id), String.Join(", ", parts(id)), "")
        Next
    End Sub
End Module

''' <summary>Who is logged in.</summary>
Public Module Session
    Public UserId As Integer
    Public Username As String = ""
    Public Role As String = ""

    Public Sub Start(id As Integer, name As String, userRole As String)
        UserId = id
        Username = name
        Role = userRole
    End Sub

    Public Sub Clear()
        UserId = 0
        Username = ""
        Role = ""
    End Sub

    ' "Remember me" keeps only the username in %AppData%\FoodHub\remember.txt
    Private Function RememberPath() As String
        Dim dir As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FoodHub")
        Return Path.Combine(dir, "remember.txt")
    End Function

    Public Function LoadRemembered() As String
        Try
            If File.Exists(RememberPath()) Then Return File.ReadAllText(RememberPath()).Trim()
        Catch
        End Try
        Return ""
    End Function

    Public Sub SaveRemembered(name As String)
        Try
            Dim p As String = RememberPath()
            Directory.CreateDirectory(Path.GetDirectoryName(p))
            If name = "" Then
                If File.Exists(p) Then File.Delete(p)
            Else
                File.WriteAllText(p, name)
            End If
        Catch
        End Try
    End Sub
End Module

''' <summary>Input rules used by the register / seller forms.</summary>
Public Module Validator
    Public Function UsernameFormatOk(u As String) As Boolean
        Return Regex.IsMatch(u, "^[A-Za-z0-9_]{4,20}$")
    End Function

    Public Function PasswordLengthOk(p As String) As Boolean
        Return p.Length >= 6
    End Function

    Public Function PasswordHasCapital(p As String) As Boolean
        Return Regex.IsMatch(p, "[A-Z]")
    End Function

    Public Function PasswordHasNumber(p As String) As Boolean
        Return Regex.IsMatch(p, "[0-9]")
    End Function

    ''' <summary>Philippine mobile: 09XXXXXXXXX or +639XXXXXXXXX</summary>
    Public Function PhoneOk(p As String) As Boolean
        Return Regex.IsMatch(p, "^(09\d{9}|\+639\d{9})$")
    End Function

    Public Function PhoneStartOk(p As String) As Boolean
        Return p.StartsWith("09") OrElse p.StartsWith("+63")
    End Function
End Module

Public Module AuthSvc
    ''' <summary>Account status found by the last Login call once the password was right (active, pending, rejected, disabled); empty if the login failed earlier.</summary>
    Public LastLoginStatus As String = ""

    ''' <summary>Stall name of a seller account (used by the waiting-for-approval screen).</summary>
    Public Function PendingStallName(username As String) As String
        Try
            Dim o As Object = Db.Scalar("SELECT TOP 1 s.[name] FROM stalls s INNER JOIN users u ON u.id = s.owner_id WHERE u.username=@u", Db.P("@u", username))
            If o Is Nothing OrElse IsDBNull(o) Then Return ""
            Return Convert.ToString(o)
        Catch
            Return ""
        End Try
    End Function

    Public Function UsernameTaken(u As String) As Boolean
        Dim n As Object = Db.Scalar("SELECT COUNT(*) FROM users WHERE username=@u", Db.P("@u", u))
        Return Convert.ToInt32(n) > 0
    End Function

    Public Function StallNameTaken(n As String) As Boolean
        Dim c As Object = Db.Scalar("SELECT COUNT(*) FROM stalls WHERE [name]=@n", Db.P("@n", n))
        Return Convert.ToInt32(c) > 0
    End Function

    Public Function Login(username As String, password As String, ByRef errMsg As String) As Boolean
        LastLoginStatus = ""
        Dim dt As DataTable = Db.Query("SELECT TOP 1 id, username, password_hash, [role], [status] FROM users WHERE username=@u", Db.P("@u", username))
        If dt.Rows.Count = 0 Then
            errMsg = "Invalid username or password."
            Return False
        End If
        Dim row As DataRow = dt.Rows(0)
        If Not BCrypt.Net.BCrypt.Verify(password, Db.GetStr(row, "password_hash")) Then
            errMsg = "Invalid username or password."
            Return False
        End If
        LastLoginStatus = Db.GetStr(row, "status")
        Select Case Db.GetStr(row, "status")
            Case "pending"
                errMsg = "Your seller application is still waiting for admin approval."
                Return False
            Case "rejected"
                errMsg = "Your seller application was not approved."
                Return False
            Case "disabled"
                errMsg = "This account has been disabled."
                Return False
        End Select
        Session.Start(Db.GetInt(row, "id"), Db.GetStr(row, "username"), Db.GetStr(row, "role"))
        Return True
    End Function

    Public Function Register(username As String, password As String, ByRef errMsg As String) As Boolean
        Try
            If UsernameTaken(username) Then
                errMsg = "That username is already taken."
                Return False
            End If
            Dim hash As String = BCrypt.Net.BCrypt.HashPassword(password, 10)
            Db.Exec("INSERT INTO users (username, password_hash, [role], [status], created_at) VALUES (@u,@p,'customer','active',@t)", Db.P("@u", username), Db.P("@p", hash), Db.P("@t", DateTime.Now))
            Return True
        Catch ex As Exception
            errMsg = "Could not create the account: " & ex.Message
            Return False
        End Try
    End Function

    ''' <summary>Creates a seller account (status pending) together with its stall. An admin must approve it before it can log in.</summary>
    Public Function ApplySeller(username As String, password As String, stallName As String, contact As String, ByRef errMsg As String) As Boolean
        Try
            If UsernameTaken(username) Then
                errMsg = "That username is already taken."
                Return False
            End If
            If StallNameTaken(stallName) Then
                errMsg = "That stall name is already registered."
                Return False
            End If
            Dim hash As String = BCrypt.Net.BCrypt.HashPassword(password, 10)
            Using cn As OleDbConnection = Db.Open()
                Using tx As OleDbTransaction = cn.BeginTransaction()
                    Try
                        Dim uid As Long = Db.ExecOn(cn, tx, "INSERT INTO users (username, password_hash, [role], [status], created_at) VALUES (@u,@p,'seller','pending',@t)", Db.P("@u", username), Db.P("@p", hash), Db.P("@t", DateTime.Now))
                        ' first rent is due 1 month from today (this used to be a MySQL trigger)
                        Db.ExecOn(cn, tx, "INSERT INTO stalls (owner_id, [name], contact, is_open, rent_due_date) VALUES (@o,@n,@c,0,@d)", Db.P("@o", uid), Db.P("@n", stallName), Db.P("@c", contact), Db.P("@d", DateTime.Today.AddMonths(1)))
                        tx.Commit()
                    Catch ex As Exception
                        tx.Rollback()
                        Throw
                    End Try
                End Using
            End Using
            Return True
        Catch ex As Exception
            errMsg = "Could not send the request: " & ex.Message
            Return False
        End Try
    End Function
End Module

Public Module CatalogSvc
    ''' <summary>Lets a typed search text be used safely inside a LIKE pattern ([ is special in Access).</summary>
    Public Function LikePattern(q As String) As String
        Return "%" & If(q, "").Replace("[", "[[]") & "%"
    End Function

    Public Function GetStallCategories() As DataTable
        Return Db.Query("SELECT id, [name] FROM stall_categories ORDER BY [name]")
    End Function

    Public Function GetStalls(search As String, categoryId As Integer) As DataTable
        Dim q As String = If(search, "").Trim()
        Dim sql As String = "SELECT s.id, s.[name], IIf(c.[name] IS NULL, 'Food', c.[name]) AS category, s.logo_path, s.cover_path, s.is_open " &
                            "FROM (stalls s INNER JOIN users u ON u.id = s.owner_id) " &
                            "LEFT JOIN stall_categories c ON c.id = s.category_id " &
                            "WHERE u.[status]='active'"
        If q <> "" Then sql &= " AND (s.[name] LIKE @like OR c.[name] LIKE @like)"
        If categoryId <> 0 Then sql &= " AND s.category_id = @cat"
        sql &= " ORDER BY s.is_open DESC, s.[name]"
        Return Db.Query(sql, Db.P("@like", LikePattern(q)), Db.P("@cat", categoryId))
    End Function

    Public Function GetStall(stallId As Integer) As DataRow
        Dim dt As DataTable = Db.Query("SELECT s.id, s.[name], IIf(c.[name] IS NULL, 'Food', c.[name]) AS category, s.logo_path, s.cover_path, s.is_open " &
                                       "FROM stalls s LEFT JOIN stall_categories c ON c.id = s.category_id WHERE s.id=@id", Db.P("@id", stallId))
        If dt.Rows.Count = 0 Then Return Nothing
        Return dt.Rows(0)
    End Function

    Public Function GetBanners() As DataTable
        Return Db.Query("SELECT b.id, b.image_path, b.title, b.stall_id, s.[name] AS stall_name " &
                        "FROM (banners b INNER JOIN stalls s ON s.id=b.stall_id) INNER JOIN users u ON u.id=s.owner_id " &
                        "WHERE b.is_active=1 AND u.[status]='active' ORDER BY b.sort_order, b.id")
    End Function

    ''' <summary>Menu category names. stallId = 0 gives the distinct names across every active stall.</summary>
    Public Function GetMenuCategoryNames(stallId As Integer) As List(Of String)
        Dim sql As String = "SELECT DISTINCT mc.[name] FROM (menu_categories mc INNER JOIN stalls s ON s.id=mc.stall_id) INNER JOIN users u ON u.id=s.owner_id " &
                            "WHERE u.[status]='active'"
        If stallId <> 0 Then sql &= " AND mc.stall_id=@sid"
        sql &= " ORDER BY mc.[name]"
        Dim dt As DataTable = Db.Query(sql, Db.P("@sid", stallId))
        Dim list As New List(Of String)
        For Each r As DataRow In dt.Rows
            list.Add(Db.GetStr(r, "name"))
        Next
        Return list
    End Function

    Public Function GetProducts(stallId As Integer, menuCategory As String, search As String) As DataTable
        Dim q As String = If(search, "").Trim()
        Dim cat As String = If(menuCategory, "")
        Dim sql As String = "SELECT p.id, p.stall_id, s.[name] AS stall_name, s.is_open, p.[name], p.short_desc, p.image_path, p.base_price, p.is_available, " &
                            "IIf((SELECT COUNT(*) FROM product_sizes z WHERE z.product_id=p.id)>0, (SELECT MIN(z.price) FROM product_sizes z WHERE z.product_id=p.id), p.base_price) AS from_price, " &
                            "(SELECT COUNT(*) FROM product_sizes z WHERE z.product_id=p.id) AS size_count, " &
                            "(SELECT COUNT(*) FROM product_extras e WHERE e.product_id=p.id) AS extra_count, " &
                            "(SELECT AVG(r.rating) FROM reviews r WHERE r.product_id=p.id) AS avg_rating, " &
                            "(SELECT COUNT(*) FROM reviews r WHERE r.product_id=p.id) AS review_count " &
                            "FROM ((products p INNER JOIN stalls s ON s.id=p.stall_id) INNER JOIN users u ON u.id=s.owner_id) " &
                            "LEFT JOIN menu_categories mc ON mc.id=p.menu_category_id " &
                            "WHERE u.[status]='active'"
        If stallId <> 0 Then sql &= " AND p.stall_id=@sid"
        If cat <> "" Then sql &= " AND mc.[name]=@cat"
        If q <> "" Then sql &= " AND (p.[name] LIKE @like OR p.short_desc LIKE @like)"
        sql &= " ORDER BY p.is_available DESC, p.[name]"
        Return Db.Query(sql, Db.P("@sid", stallId), Db.P("@cat", cat), Db.P("@like", LikePattern(q)))
    End Function

    Public Function GetProduct(productId As Integer) As DataRow
        Dim dt As DataTable = Db.Query("SELECT p.id, p.stall_id, s.[name] AS stall_name, s.is_open, p.[name], p.short_desc, p.[description], p.image_path, p.base_price, p.is_available, " &
                                       "IIf(mc.[name] IS NULL, 'Menu', mc.[name]) AS menu_category, " &
                                       "(SELECT AVG(r.rating) FROM reviews r WHERE r.product_id=p.id) AS avg_rating, " &
                                       "(SELECT COUNT(*) FROM reviews r WHERE r.product_id=p.id) AS review_count " &
                                       "FROM (products p INNER JOIN stalls s ON s.id=p.stall_id) LEFT JOIN menu_categories mc ON mc.id=p.menu_category_id WHERE p.id=@id", Db.P("@id", productId))
        If dt.Rows.Count = 0 Then Return Nothing
        Return dt.Rows(0)
    End Function

    Public Function GetSizes(productId As Integer) As DataTable
        Return Db.Query("SELECT id, [name], price FROM product_sizes WHERE product_id=@p ORDER BY price, id", Db.P("@p", productId))
    End Function

    Public Function GetExtras(productId As Integer) As DataTable
        Return Db.Query("SELECT id, [name], price FROM product_extras WHERE product_id=@p ORDER BY price, id", Db.P("@p", productId))
    End Function
End Module

Public Module CartSvc
    ''' <summary>mode = "order" (shown in the Your order panel) or "saved" (shown on the Cart page).</summary>
    Public Function GetRows(mode As String) As DataTable
        Dim flag As String = If(mode = "saved", "ci.is_saved=1", "ci.in_order=1")
        Dim dt As DataTable = Db.Query("SELECT ci.id AS cart_id, ci.product_id, p.stall_id, s.[name] AS stall_name, s.logo_path, s.is_open, p.[name] AS product_name, p.image_path, p.is_available, " &
                        "z.[name] AS size_name, IIf(z.price IS NULL, p.base_price, z.price) AS unit_price, ci.dine_option, ci.quantity, ci.is_checked, ci.is_saved, ci.in_order, " &
                        "(SELECT SUM(e.price) FROM cart_item_extras x INNER JOIN product_extras e ON e.id=x.extra_id WHERE x.cart_item_id=ci.id) AS extras_total " &
                        "FROM ((cart_items ci INNER JOIN products p ON p.id=ci.product_id) INNER JOIN stalls s ON s.id=p.stall_id) " &
                        "LEFT JOIN product_sizes z ON z.id=ci.size_id " &
                        "WHERE ci.customer_id=@c AND " & flag & " ORDER BY s.[name], ci.id", Db.P("@c", Session.UserId))
        Db.AttachJoinedText(dt, "cart_id", "extras_text",
                            "SELECT x.cart_item_id AS k, e.[name] AS t FROM cart_item_extras x INNER JOIN product_extras e ON e.id=x.extra_id " &
                            "WHERE x.cart_item_id IN ({IDS}) ORDER BY e.[name]", "k", "t")
        Return dt
    End Function

    Private Function ExtrasKey(extraIds As List(Of Integer)) As String
        Dim sorted As New List(Of Integer)(extraIds)
        sorted.Sort()
        Return String.Join(",", sorted)
    End Function

    ''' <summary>Deletes cart rows (and their extras links first: Access does not cascade for us).</summary>
    Public Sub DeleteCartRows(whereSql As String, ParamArray args() As OleDbParameter)
        Db.Exec("DELETE FROM cart_item_extras WHERE cart_item_id IN (SELECT id FROM cart_items WHERE " & whereSql & ")", args)
        Db.Exec("DELETE FROM cart_items WHERE " & whereSql, args)
    End Sub

    ''' <summary>Finds an existing cart row with the same product/size/dine option/extras that has the given flag (in_order or is_saved).</summary>
    Public Function FindMatch(productId As Integer, sizeId As Integer, dine As String, extraIds As List(Of Integer), flagColumn As String) As Integer
        Dim sizeCond As String = If(sizeId = 0, "size_id IS NULL", "size_id = @s")
        Dim cand As DataTable = Db.Query("SELECT id FROM cart_items WHERE customer_id=@c AND product_id=@p AND dine_option=@d AND " & sizeCond & " AND " & flagColumn & "=1",
                                         Db.P("@c", Session.UserId), Db.P("@p", productId), Db.P("@d", dine), Db.P("@s", sizeId))
        Dim key As String = ExtrasKey(extraIds)
        For Each r As DataRow In cand.Rows
            Dim id As Integer = Db.GetInt(r, "id")
            Dim ex As DataTable = Db.Query("SELECT extra_id FROM cart_item_extras WHERE cart_item_id=@i", Db.P("@i", id))
            Dim ids As New List(Of Integer)
            For Each er As DataRow In ex.Rows
                ids.Add(Db.GetInt(er, "extra_id"))
            Next
            If ExtrasKey(ids) = key Then Return id
        Next
        Return 0
    End Function

    Private Function InsertRow(productId As Integer, sizeId As Integer, dine As String, qty As Integer, extraIds As List(Of Integer), inOrder As Boolean, isSaved As Boolean) As Integer
        Dim newId As Long = Db.Insert("INSERT INTO cart_items (customer_id, product_id, size_id, dine_option, quantity, in_order, is_saved, is_checked) VALUES (@c,@p,@s,@d,@q,@o,@v,1)",
                                      Db.P("@c", Session.UserId), Db.P("@p", productId), Db.P("@s", Db.NullIfZero(sizeId)), Db.P("@d", dine), Db.P("@q", qty),
                                      Db.P("@o", If(inOrder, 1, 0)), Db.P("@v", If(isSaved, 1, 0)))
        For Each ex As Integer In extraIds
            Db.Exec("INSERT INTO cart_item_extras (cart_item_id, extra_id) VALUES (@i,@e)", Db.P("@i", newId), Db.P("@e", ex))
        Next
        Return CInt(newId)
    End Function

    ''' <summary>Add to the Your order panel (merges with an identical line).</summary>
    Public Sub AddToOrder(productId As Integer, sizeId As Integer, dine As String, qty As Integer, extraIds As List(Of Integer))
        Dim existing As Integer = FindMatch(productId, sizeId, dine, extraIds, "in_order")
        If existing > 0 Then
            Db.Exec("UPDATE cart_items SET quantity = IIf(quantity + @q > 99, 99, quantity + @q), is_checked=1 WHERE id=@i AND customer_id=@c", Db.P("@q", qty), Db.P("@i", existing), Db.P("@c", Session.UserId))
        Else
            InsertRow(productId, sizeId, dine, qty, extraIds, True, False)
        End If
    End Sub

    ''' <summary>Bookmark: keep this configuration on the Cart page.</summary>
    Public Sub SaveToCart(productId As Integer, sizeId As Integer, dine As String, qty As Integer, extraIds As List(Of Integer))
        If FindMatch(productId, sizeId, dine, extraIds, "is_saved") = 0 Then
            InsertRow(productId, sizeId, dine, qty, extraIds, False, True)
        End If
    End Sub

    Public Function SavedId(productId As Integer, sizeId As Integer, dine As String, extraIds As List(Of Integer)) As Integer
        Return FindMatch(productId, sizeId, dine, extraIds, "is_saved")
    End Function

    Private Sub CleanUp(cartId As Integer)
        DeleteCartRows("id=@i AND customer_id=@c AND in_order=0 AND is_saved=0", Db.P("@i", cartId), Db.P("@c", Session.UserId))
    End Sub

    Public Sub SetQty(cartId As Integer, qty As Integer)
        Db.Exec("UPDATE cart_items SET quantity=@q WHERE id=@i AND customer_id=@c", Db.P("@q", Math.Max(1, Math.Min(99, qty))), Db.P("@i", cartId), Db.P("@c", Session.UserId))
    End Sub

    Public Sub SetChecked(cartId As Integer, checked As Boolean)
        Db.Exec("UPDATE cart_items SET is_checked=@v WHERE id=@i AND customer_id=@c", Db.P("@v", If(checked, 1, 0)), Db.P("@i", cartId), Db.P("@c", Session.UserId))
    End Sub

    Public Sub SetAllChecked(checked As Boolean)
        Db.Exec("UPDATE cart_items SET is_checked=@v WHERE customer_id=@c AND in_order=1", Db.P("@v", If(checked, 1, 0)), Db.P("@c", Session.UserId))
    End Sub

    Public Sub SetInOrder(cartId As Integer, value As Boolean)
        Db.Exec("UPDATE cart_items SET in_order=@v, is_checked=1 WHERE id=@i AND customer_id=@c", Db.P("@v", If(value, 1, 0)), Db.P("@i", cartId), Db.P("@c", Session.UserId))
        If Not value Then CleanUp(cartId)
    End Sub

    Public Sub SetSaved(cartId As Integer, value As Boolean)
        Db.Exec("UPDATE cart_items SET is_saved=@v WHERE id=@i AND customer_id=@c", Db.P("@v", If(value, 1, 0)), Db.P("@i", cartId), Db.P("@c", Session.UserId))
        If Not value Then CleanUp(cartId)
    End Sub

    Public Sub DeleteItem(cartId As Integer)
        DeleteCartRows("id=@i AND customer_id=@c", Db.P("@i", cartId), Db.P("@c", Session.UserId))
    End Sub

    Public Function LineTotal(r As DataRow) As Decimal
        Return (Db.GetDec(r, "unit_price") + Db.GetDec(r, "extras_total")) * Db.GetInt(r, "quantity")
    End Function
End Module

Public Module OrderSvc
    Private Function Clip(s As String, maxLen As Integer) As String
        If s Is Nothing Then Return Nothing
        If s = "" Then Return Nothing
        If s.Length > maxLen Then Return s.Substring(0, maxLen)
        Return s
    End Function

    ''' <summary>Turns every ticked item in the Your order panel into a real order (one sub-order per stall).</summary>
    Public Function PlaceOrder(ByRef orderId As Long, ByRef errMsg As String) As Boolean
        Dim rows As DataTable = CartSvc.GetRows("order")
        Dim picked As New List(Of DataRow)
        For Each r As DataRow In rows.Rows
            If Db.GetBool(r, "is_checked") Then picked.Add(r)
        Next
        If picked.Count = 0 Then
            errMsg = "Tick at least one item in your order first."
            Return False
        End If
        For Each r As DataRow In picked
            If Not Db.GetBool(r, "is_available") Then
                errMsg = Db.GetStr(r, "product_name") & " is not available right now."
                Return False
            End If
            If Not Db.GetBool(r, "is_open") Then
                errMsg = Db.GetStr(r, "stall_name") & " is closed right now."
                Return False
            End If
        Next
        ' stock check (items that track stock)
        Dim want As New Dictionary(Of Integer, Integer)
        For Each r As DataRow In picked
            Dim wp As Integer = Db.GetInt(r, "product_id")
            want(wp) = If(want.ContainsKey(wp), want(wp), 0) + Db.GetInt(r, "quantity")
        Next
        Dim tracked As New HashSet(Of Integer)
        For Each kv As KeyValuePair(Of Integer, Integer) In want
            Dim pr As DataTable = Db.Query("SELECT [name], track_stock, stock FROM products WHERE id=@p", Db.P("@p", kv.Key))
            If pr.Rows.Count = 0 OrElse Not Db.GetBool(pr.Rows(0), "track_stock") Then Continue For
            tracked.Add(kv.Key)
            Dim left As Integer = Db.GetInt(pr.Rows(0), "stock")
            If left < kv.Value Then
                If left <= 0 Then
                    errMsg = Db.GetStr(pr.Rows(0), "name") & " is sold out."
                Else
                    errMsg = "Only " & left.ToString() & " left of " & Db.GetStr(pr.Rows(0), "name") & "."
                End If
                Return False
            End If
        Next
        Dim total As Decimal = 0D
        For Each r As DataRow In picked
            total += CartSvc.LineTotal(r)
        Next
        Dim stallIds As New List(Of Integer)
        For Each r As DataRow In picked
            Dim sid As Integer = Db.GetInt(r, "stall_id")
            If Not stallIds.Contains(sid) Then stallIds.Add(sid)
        Next
        Try
            Using cn As OleDbConnection = Db.Open()
                Using tx As OleDbTransaction = cn.BeginTransaction()
                    Try
                        Dim stamp As DateTime = DateTime.Now
                        orderId = Db.ExecOn(cn, tx, "INSERT INTO orders (customer_id, total, created_at) VALUES (@c,@t,@n)", Db.P("@c", Session.UserId), Db.P("@t", total), Db.P("@n", stamp))
                        For Each sid As Integer In stallIds
                            Dim stallSub As Decimal = 0D
                            For Each r As DataRow In picked
                                If Db.GetInt(r, "stall_id") = sid Then stallSub += CartSvc.LineTotal(r)
                            Next
                            Dim osId As Long = Db.ExecOn(cn, tx, "INSERT INTO order_stalls (order_id, stall_id, [status], subtotal, updated_at) VALUES (@o,@s,'pending',@t,@u)", Db.P("@o", orderId), Db.P("@s", sid), Db.P("@t", stallSub), Db.P("@u", stamp))
                            For Each r As DataRow In picked
                                If Db.GetInt(r, "stall_id") <> sid Then Continue For
                                Db.ExecOn(cn, tx, "INSERT INTO order_items (order_stall_id, product_id, product_name, size_name, extras_text, dine_option, quantity, unit_price, extras_total) VALUES (@os,@p,@n,@z,@x,@d,@q,@u,@e)",
                                          Db.P("@os", osId), Db.P("@p", Db.GetInt(r, "product_id")), Db.P("@n", Db.GetStr(r, "product_name")),
                                          Db.P("@z", Clip(Db.GetStr(r, "size_name"), 30)),
                                          Db.P("@x", Clip(Db.GetStr(r, "extras_text"), 255)),
                                          Db.P("@d", Db.GetStr(r, "dine_option")), Db.P("@q", Db.GetInt(r, "quantity")),
                                          Db.P("@u", Db.GetDec(r, "unit_price")), Db.P("@e", Db.GetDec(r, "extras_total")))
                            Next
                        Next
                        For Each r As DataRow In picked
                            Dim cid As Integer = Db.GetInt(r, "cart_id")
                            If Db.GetBool(r, "is_saved") Then
                                Db.ExecOn(cn, tx, "UPDATE cart_items SET in_order=0 WHERE id=@i", Db.P("@i", cid))
                            Else
                                Db.ExecOn(cn, tx, "DELETE FROM cart_item_extras WHERE cart_item_id=@i", Db.P("@i", cid))
                                Db.ExecOn(cn, tx, "DELETE FROM cart_items WHERE id=@i", Db.P("@i", cid))
                            End If
                        Next
                        For Each kv As KeyValuePair(Of Integer, Integer) In want
                            If Not tracked.Contains(kv.Key) Then Continue For
                            Dim changed As Integer = Db.ExecCountOn(cn, tx, "UPDATE products SET is_available = IIf(stock-@q<=0, 0, is_available), stock = stock-@q WHERE id=@p AND track_stock=1 AND stock>=@q",
                                                                    Db.P("@q", kv.Value), Db.P("@p", kv.Key))
                            If changed = 0 Then Throw New InvalidOperationException("Someone just ordered the last of an item. Please check your order and try again.")
                        Next
                        tx.Commit()
                    Catch
                        tx.Rollback()
                        Throw
                    End Try
                End Using
            End Using
            Return True
        Catch ex As Exception
            errMsg = "Could not place the order: " & ex.Message
            Return False
        End Try
    End Function

    ''' <summary>All of the customer's orders, oldest first. open_count = stalls still pending/preparing/ready.</summary>
    Public Function GetOrders() As DataTable
        Return Db.Query("SELECT o.id, o.created_at, o.total, " &
                        "(SELECT COUNT(*) FROM order_stalls os WHERE os.order_id=o.id) AS stall_count, " &
                        "(SELECT COUNT(*) FROM order_stalls os WHERE os.order_id=o.id AND os.[status] IN ('pending','preparing','ready')) AS open_count " &
                        "FROM orders o WHERE o.customer_id=@c ORDER BY o.id ASC", Db.P("@c", Session.UserId))
    End Function

    Public Function GetOrder(orderId As Long) As DataRow
        Dim dt As DataTable = Db.Query("SELECT id, created_at, total FROM orders WHERE id=@o AND customer_id=@c", Db.P("@o", orderId), Db.P("@c", Session.UserId))
        If dt.Rows.Count = 0 Then Return Nothing
        Return dt.Rows(0)
    End Function

    Public Function GetOrderStalls(orderId As Long) As DataTable
        Return Db.Query("SELECT os.id, os.stall_id, s.[name] AS stall_name, s.logo_path, os.[status], os.subtotal " &
                        "FROM order_stalls os INNER JOIN stalls s ON s.id=os.stall_id WHERE os.order_id=@o ORDER BY s.[name]", Db.P("@o", orderId))
    End Function

    Public Function GetOrderItems(orderStallId As Integer) As DataTable
        Return Db.Query("SELECT product_name, size_name, extras_text, dine_option, quantity, unit_price, extras_total FROM order_items WHERE order_stall_id=@os ORDER BY id", Db.P("@os", orderStallId))
    End Function

    Public Function GetMessages(orderStallId As Integer) As DataTable
        Return Db.Query("SELECT m.body, m.sent_at, m.sender_id, u.[role] FROM messages m INNER JOIN users u ON u.id=m.sender_id WHERE m.order_stall_id=@os ORDER BY m.id", Db.P("@os", orderStallId))
    End Function

    Public Sub SendMessage(orderStallId As Integer, body As String)
        Db.Exec("INSERT INTO messages (order_stall_id, sender_id, body, sent_at, is_read) VALUES (@os,@u,@b,@t,0)",
                Db.P("@os", orderStallId), Db.P("@u", Session.UserId), Db.P("@b", body), Db.P("@t", DateTime.Now))
    End Sub
End Module
