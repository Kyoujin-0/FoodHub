Imports System.Data
Imports System.Globalization

''' <summary>One stall's rent for one month (what the admin Rent list shows).</summary>
Public Class RentRow
    Public StallId As Integer
    Public StallName As String = ""
    Public Owner As String = ""
    Public Rent As Decimal
    Public Paid As Decimal
    Public Balance As Decimal
    ''' <summary>Due date of this month's rent.</summary>
    Public DueDate As Date
    ''' <summary>How many months after the stall's first due date this month's rent is due (0 = the first one).</summary>
    Public DueOffsetMonths As Integer
    ''' <summary>Photo to show: the owner's profile picture, else the stall logo ("" = none, initials are shown).</summary>
    Public PhotoPath As String = ""
    ''' <summary>Date of the last payment for this month (Date.MinValue = none).</summary>
    Public LastPaid As Date
    ''' <summary>paid | partial | duesoon | overdue</summary>
    Public Status As String = "duesoon"
End Class

''' <summary>
''' Rent rules: one flat monthly rent for every stall (app_settings.monthly_rent).
''' A stall's rent starts in the month its owner account was created. The first rent is due on
''' stalls.rent_due_date (default: 1 month after the account was created, the admin can change it);
''' the rent of each later month is due on the same day of its own month.
''' Paid = nothing left to pay. Overdue = due date passed and something is still unpaid.
''' Partial = some paid, not late yet. Due soon = nothing paid, not late yet.
''' </summary>
Public Module RentSvc
    Public Function Money(d As Decimal) As String
        Return ChrW(&H20B1) & d.ToString("#,##0.##", CultureInfo.InvariantCulture)
    End Function

    Public Function RentStatusText(key As String) As String
        Select Case key
            Case "paid" : Return "Paid"
            Case "partial" : Return "Partial"
            Case "overdue" : Return "Overdue"
            Case Else : Return "Due soon"
        End Select
    End Function

    ''' <summary>Owner's profile picture if there is one, otherwise the stall logo.</summary>
    Public Function PhotoOf(avatar As String, logo As String) As String
        If If(avatar, "") <> "" Then Return avatar
        Return If(logo, "")
    End Function

    ''' <summary>Current photo path of every stall (stall id -> path), read fresh from the database.</summary>
    Public Function GetPhotos() As Dictionary(Of Integer, String)
        Dim map As New Dictionary(Of Integer, String)
        Dim dt As DataTable = Db.Query("SELECT s.id, s.logo_path, u.avatar_path FROM stalls s INNER JOIN users u ON u.id = s.owner_id")
        For Each r As DataRow In dt.Rows
            map(Db.GetInt(r, "id")) = PhotoOf(Db.GetStr(r, "avatar_path"), Db.GetStr(r, "logo_path"))
        Next
        Return map
    End Function

    Public Function MonthStart(d As Date) As Date
        Return New Date(d.Year, d.Month, 1)
    End Function

    Public Function GetMonthlyRent() As Decimal
        Dim o As Object = Db.Scalar("SELECT setting_value FROM app_settings WHERE setting_key='monthly_rent'")
        Dim v As Decimal
        If o IsNot Nothing AndAlso Not IsDBNull(o) AndAlso
           Decimal.TryParse(Convert.ToString(o), NumberStyles.Number, CultureInfo.InvariantCulture, v) Then
            Return v
        End If
        Return 5000D
    End Function

    Public Sub SetMonthlyRent(amount As Decimal)
        Dim v As String = amount.ToString("0.##", CultureInfo.InvariantCulture)
        Dim n As Integer = Db.Exec("UPDATE app_settings SET setting_value=@v WHERE setting_key='monthly_rent'", Db.P("@v", v))
        If n = 0 Then
            Db.Exec("INSERT INTO app_settings (setting_key, setting_value) VALUES ('monthly_rent', @v)", Db.P("@v", v))
        End If
    End Sub

    ''' <summary>First month any active stall owner has an account (the month picker cannot go earlier).</summary>
    Public Function EarliestMonth() As Date
        Dim o As Object = Db.Scalar("SELECT MIN(u.created_at) FROM users u INNER JOIN stalls s ON s.owner_id=u.id WHERE u.[status]='active'")
        If o Is Nothing OrElse IsDBNull(o) Then Return MonthStart(Date.Today)
        Return MonthStart(Convert.ToDateTime(o))
    End Function

    ''' <summary>Rent of every active stall for the given month.</summary>
    Public Function GetRows(month As Date) As List(Of RentRow)
        month = MonthStart(month)
        Dim rent As Decimal = GetMonthlyRent()
        Dim today As Date = Date.Today
        Dim dt As DataTable = Db.Query(
            "SELECT s.id, s.[name], u.username, u.created_at, s.rent_due_date, s.logo_path, u.avatar_path, " &
            "       (SELECT SUM(p.amount) FROM rent_payments p WHERE p.stall_id = s.id AND p.rent_month = @m) AS paid, " &
            "       (SELECT MAX(p.paid_at) FROM rent_payments p WHERE p.stall_id = s.id AND p.rent_month = @m) AS last_paid " &
            "FROM stalls s INNER JOIN users u ON u.id = s.owner_id " &
            "WHERE u.[status] = 'active' " &
            "ORDER BY s.[name]", Db.P("@m", month))
        Dim list As New List(Of RentRow)
        For Each r As DataRow In dt.Rows
            Dim created As Date = Db.GetDate(r, "created_at")
            Dim startMonth As Date = MonthStart(created)
            If month < startMonth Then Continue For

            Dim anchor As Date = Db.GetDate(r, "rent_due_date")
            If anchor = Date.MinValue Then anchor = created.Date.AddMonths(1)
            Dim offs As Integer = (month.Year - startMonth.Year) * 12 + month.Month - startMonth.Month
            Dim due As Date = anchor.AddMonths(offs)

            Dim paid As Decimal = Db.GetDec(r, "paid")
            Dim balance As Decimal = Math.Max(0D, rent - paid)
            Dim status As String
            If balance <= 0D Then
                status = "paid"
            ElseIf today > due.Date Then
                status = "overdue"
            ElseIf paid > 0D Then
                status = "partial"
            Else
                status = "duesoon"
            End If

            Dim row As New RentRow()
            row.StallId = Db.GetInt(r, "id")
            row.StallName = Db.GetStr(r, "name")
            row.Owner = Db.GetStr(r, "username")
            row.PhotoPath = PhotoOf(Db.GetStr(r, "avatar_path"), Db.GetStr(r, "logo_path"))
            row.Rent = rent
            row.Paid = paid
            row.Balance = balance
            row.DueDate = due.Date
            row.DueOffsetMonths = offs
            row.LastPaid = Db.GetDate(r, "last_paid")
            row.Status = status
            list.Add(row)
        Next
        Return list
    End Function

    ''' <summary>All payments recorded for the given month, newest first (History).</summary>
    Public Function GetPayments(month As Date) As DataTable
        Return Db.Query(
            "SELECT p.id, p.stall_id, s.[name] AS stall, u.username AS owner, u.avatar_path AS avatar, s.logo_path AS logo, p.amount, p.paid_at, a.username AS recorded_by " &
            "FROM ((rent_payments p " &
            "INNER JOIN stalls s ON s.id = p.stall_id) " &
            "INNER JOIN users u ON u.id = s.owner_id) " &
            "LEFT JOIN users a ON a.id = p.recorded_by " &
            "WHERE p.rent_month = @m ORDER BY p.paid_at DESC, p.id DESC", Db.P("@m", MonthStart(month)))
    End Function

    ''' <summary>Records a cash payment. Refuses zero and amounts above what is still owed.</summary>
    Public Function RecordPayment(stallId As Integer, month As Date, amount As Decimal, ByRef errMsg As String) As Boolean
        Try
            If amount <= 0D Then
                errMsg = "Enter an amount greater than zero."
                Return False
            End If
            month = MonthStart(month)
            Dim paidObj As Object = Db.Scalar(
                "SELECT SUM(amount) FROM rent_payments WHERE stall_id=@s AND rent_month=@m",
                Db.P("@s", stallId), Db.P("@m", month))
            Dim paid As Decimal = If(paidObj Is Nothing OrElse IsDBNull(paidObj), 0D, Convert.ToDecimal(paidObj))
            Dim balance As Decimal = GetMonthlyRent() - paid
            If balance <= 0D Then
                errMsg = "This stall has already paid the rent for that month."
                Return False
            End If
            If amount > balance Then
                errMsg = "That is more than the balance (" & Money(balance) & ")."
                Return False
            End If
            Db.Insert("INSERT INTO rent_payments (stall_id, rent_month, amount, paid_at, recorded_by) VALUES (@s,@m,@a,@t,@u)",
                      Db.P("@s", stallId), Db.P("@m", month), Db.P("@a", amount), Db.P("@t", DateTime.Now), Db.P("@u", Db.NullIfZero(Session.UserId)))
            Return True
        Catch ex As Exception
            errMsg = "Could not record the payment: " & ex.Message
            Return False
        End Try
    End Function

    ''' <summary>Sets the stall's FIRST due date (later months follow the same day).</summary>
    Public Sub SetFirstDueDate(stallId As Integer, firstDue As Date)
        Db.Exec("UPDATE stalls SET rent_due_date=@d WHERE id=@s", Db.P("@d", firstDue.Date), Db.P("@s", stallId))
    End Sub
End Module
