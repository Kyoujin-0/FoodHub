Option Strict Off
Option Infer On
Imports System.Collections
Imports System.Collections.Generic
Imports System.Data
Imports System.Globalization
Imports System.Linq

' =====================================================================
' Core/FloorSvc.vb : data for the admin Stores tab (floor map)
'   Tables: floor_slots (created automatically), stalls.slot_no (added automatically)
'   Settings (app_settings): floor_width, floor_height, floor_next_slot
'   Slot numbers are never reused (floor_next_slot only goes up).
' =====================================================================

''' <summary>One rectangle on the floor: a stall slot or the dining area.</summary>
Public Class FloorSlot
    Public Property Id As Integer                 ' floor_slots.id (0 = not saved yet)
    Public Property Slot As Integer               ' slot number (0 for the dining area)
    Public Property Kind As String = "stall"      ' stall | dining
    Public Property X As Integer
    Public Property Y As Integer
    Public Property W As Integer
    Public Property H As Integer
    Public Property Face As String = "s"          ' side that opens toward the dining area: n e s w

    Public ReadOnly Property IsDining As Boolean
        Get
            Return Kind = "dining"
        End Get
    End Property

    Public Function Clone() As FloorSlot
        Return CType(MemberwiseClone(), FloorSlot)
    End Function
End Class

Public Class FloorLayout
    Public Property Width As Integer = 1000
    Public Property Height As Integer = 828
    Public Property NextSlot As Integer = 1
    Public Property Slots As New List(Of FloorSlot)

    Public Function Clone() As FloorLayout
        Dim c As New FloorLayout With {.Width = Width, .Height = Height, .NextSlot = NextSlot}
        For Each s In Slots
            c.Slots.Add(s.Clone())
        Next
        Return c
    End Function

    Public Function StallSlots() As List(Of FloorSlot)
        Return Slots.Where(Function(q) Not q.IsDining).ToList()
    End Function

    Public Function FindSlot(slotNo As Integer) As FloorSlot
        Return Slots.FirstOrDefault(Function(q) Not q.IsDining AndAlso q.Slot = slotNo)
    End Function

    Public Function Signature() As String
        Dim sb As New System.Text.StringBuilder()
        sb.Append(Width).Append("x").Append(Height).Append("|")
        For Each s In Slots.OrderBy(Function(q) q.Slot)
            sb.Append(s.Id).Append(",").Append(s.Slot).Append(",").Append(s.Kind).Append(",")
            sb.Append(s.X).Append(",").Append(s.Y).Append(",").Append(s.W).Append(",").Append(s.H).Append(",").Append(s.Face).Append(";")
        Next
        Return sb.ToString()
    End Function
End Class

''' <summary>An ACTIVE seller's stall as the Stores tab needs it (rent is for the current month).</summary>
Public Class FloorStall
    Public Property StallId As Integer
    Public Property Slot As Integer               ' 0 = not placed on the map
    Public Property Name As String = ""
    Public Property Category As String = ""
    Public Property Seller As String = ""
    Public Property IsOpen As Boolean
    Public Property LogoPath As String = ""
    Public Property RentStatus As String = "paid"   ' paid | partial | unpaid | overdue
    Public Property Rent As Decimal
    Public Property Paid As Decimal
    Public Property Balance As Decimal
    Public Property DueDate As Date
    Public Property DaysLate As Integer

    Public Overrides Function ToString() As String
        Return Name
    End Function
End Class

Public Module FloorSvc
    Private _ready As Boolean

    ' ---------------- small helpers ----------------

    Private Function RowsOf(result As Object) As List(Of Object)
        Dim list As New List(Of Object)
        Dim dt = TryCast(result, DataTable)
        If dt IsNot Nothing Then
            For Each r As DataRow In dt.Rows
                list.Add(r)
            Next
        ElseIf result IsNot Nothing Then
            For Each r In CType(result, IEnumerable)
                list.Add(r)
            Next
        End If
        Return list
    End Function

    Private Function ToInt(o As Object) As Integer
        If o Is Nothing OrElse IsDBNull(o) Then Return 0
        Return Convert.ToInt32(o)
    End Function

    Private Function ReadSetting(key As String, def As String) As String
        Dim o = Db.Scalar("SELECT setting_value FROM app_settings WHERE setting_key = @k", Db.P("@k", key))
        If o Is Nothing OrElse IsDBNull(o) Then Return def
        Return Convert.ToString(o)
    End Function

    Private Sub WriteSetting(key As String, value As String)
        Dim n = ToInt(Db.Scalar("SELECT COUNT(*) FROM app_settings WHERE setting_key = @k", Db.P("@k", key)))
        If n = 0 Then
            Db.Exec("INSERT INTO app_settings (setting_key, setting_value) VALUES (@k, @v)", Db.P("@k", key), Db.P("@v", value))
        Else
            Db.Exec("UPDATE app_settings SET setting_value = @v WHERE setting_key = @k", Db.P("@v", value), Db.P("@k", key))
        End If
    End Sub

    Private Function SettingInt(key As String, def As Integer) As Integer
        Dim v As Integer
        If Integer.TryParse(ReadSetting(key, def.ToString()), v) Then Return v
        Return def
    End Function

    Public Function Overlap(a As FloorSlot, b As FloorSlot) As Boolean
        Return a.X < b.X + b.W AndAlso b.X < a.X + a.W AndAlso a.Y < b.Y + b.H AndAlso b.Y < a.Y + a.H
    End Function

    ' ---------------- schema (creates what is missing, once per run) ----------------

    Public Sub EnsureSchema()
        If _ready Then Return

        Dim haveTable As Boolean = True
        Try
            Db.Scalar("SELECT COUNT(*) FROM floor_slots")
        Catch
            haveTable = False
        End Try
        If Not haveTable Then
            Db.Exec("CREATE TABLE floor_slots (id COUNTER PRIMARY KEY, slot_no LONG NOT NULL, kind TEXT(10) NOT NULL, " &
                    "pos_x LONG NOT NULL, pos_y LONG NOT NULL, size_w LONG NOT NULL, size_h LONG NOT NULL, face TEXT(1) NOT NULL)")
        End If

        Dim haveColumn As Boolean = True
        Try
            Db.Scalar("SELECT COUNT(slot_no) FROM stalls")
        Catch
            haveColumn = False
        End Try
        If Not haveColumn Then Db.Exec("ALTER TABLE stalls ADD COLUMN slot_no LONG")

        If ToInt(Db.Scalar("SELECT COUNT(*) FROM floor_slots")) = 0 Then SeedDefaultLayout()
        _ready = True
    End Sub

    Private Sub InsertShape(slotNo As Integer, kind As String, x As Integer, y As Integer, w As Integer, h As Integer, face As String)
        Db.Exec("INSERT INTO floor_slots (slot_no, kind, pos_x, pos_y, size_w, size_h, face) VALUES (@n, @k, @x, @y, @w, @h, @f)",
                Db.P("@n", slotNo), Db.P("@k", kind), Db.P("@x", x), Db.P("@y", y), Db.P("@w", w), Db.P("@h", h), Db.P("@f", face))
    End Sub

    ''' <summary>First run: a square food court with 16 slots around a dining area, and the active stalls placed in order.</summary>
    Private Sub SeedDefaultLayout()
        Dim n As Integer = 0
        For i = 0 To 3                                     ' top row, opens south
            n += 1 : InsertShape(n, "stall", i * 250, 0, 250, 150, "s")
        Next
        For i = 0 To 3                                     ' right column, opens west
            n += 1 : InsertShape(n, "stall", 780, 150 + i * 132, 220, 132, "w")
        Next
        For i = 3 To 0 Step -1                             ' bottom row, opens north
            n += 1 : InsertShape(n, "stall", i * 250, 678, 250, 150, "n")
        Next
        For i = 3 To 0 Step -1                             ' left column, opens east
            n += 1 : InsertShape(n, "stall", 0, 150 + i * 132, 220, 132, "e")
        Next
        InsertShape(0, "dining", 228, 158, 544, 512, "s")
        WriteSetting("floor_width", "1000")
        WriteSetting("floor_height", "828")
        WriteSetting("floor_next_slot", (n + 1).ToString())

        Dim slot As Integer = 0
        Dim sql = "SELECT s.id FROM (stalls AS s INNER JOIN users AS u ON s.owner_id = u.id) " &
                  "WHERE u.[status] = 'active' AND s.slot_no IS NULL ORDER BY s.id"
        For Each r In RowsOf(Db.Query(sql))
            slot += 1
            If slot > n Then Exit For
            Db.Exec("UPDATE stalls SET slot_no = @n WHERE id = @id", Db.P("@n", slot), Db.P("@id", Db.GetInt(r, "id")))
        Next
    End Sub

    ' ---------------- layout ----------------

    Public Function GetLayout() As FloorLayout
        EnsureSchema()
        Dim lay As New FloorLayout()
        lay.Width = SettingInt("floor_width", 1000)
        lay.Height = SettingInt("floor_height", 828)
        Dim maxSlot As Integer = 0
        Dim sql = "SELECT id, slot_no, kind, pos_x, pos_y, size_w, size_h, face FROM floor_slots ORDER BY slot_no"
        For Each r In RowsOf(Db.Query(sql))
            Dim s As New FloorSlot With {
                .Id = Db.GetInt(r, "id"), .Slot = Db.GetInt(r, "slot_no"), .Kind = Db.GetStr(r, "kind"),
                .X = Db.GetInt(r, "pos_x"), .Y = Db.GetInt(r, "pos_y"),
                .W = Db.GetInt(r, "size_w"), .H = Db.GetInt(r, "size_h"), .Face = Db.GetStr(r, "face")}
            If s.Face = "" Then s.Face = "s"
            lay.Slots.Add(s)
            If s.Slot > maxSlot Then maxSlot = s.Slot
        Next
        lay.NextSlot = Math.Max(SettingInt("floor_next_slot", 1), maxSlot + 1)
        Return lay
    End Function

    ''' <summary>Returns "" when the layout is fine, otherwise the problem in plain words.</summary>
    Public Function ValidateLayout(lay As FloorLayout) As String
        If lay.Width < 200 OrElse lay.Height < 200 Then Return "The floor is too small (at least 200 x 200)."
        For Each q In lay.Slots
            Dim nm = If(q.IsDining, "The dining area", "Slot " & q.Slot)
            If q.W < 40 OrElse q.H < 40 Then Return nm & " is smaller than 40 x 40."
            If q.X < 0 OrElse q.Y < 0 OrElse q.X + q.W > lay.Width OrElse q.Y + q.H > lay.Height Then
                Return nm & " sticks out of the floor."
            End If
        Next
        Dim stalls = lay.StallSlots()
        For i = 0 To stalls.Count - 1
            For j = i + 1 To stalls.Count - 1
                If Overlap(stalls(i), stalls(j)) Then Return "Slot " & stalls(i).Slot & " and slot " & stalls(j).Slot & " overlap."
            Next
        Next
        Return ""
    End Function

    ''' <summary>Validates and saves. Removing a slot is refused while an active stall still stands in it.</summary>
    Public Function SaveLayout(lay As FloorLayout, ByRef err As String) As Boolean
        err = ValidateLayout(lay)
        If err <> "" Then Return False
        Try
            EnsureSchema()
            Dim old = GetLayout()

            For Each o In old.Slots
                Dim oldId = o.Id
                If lay.Slots.Any(Function(q) q.Id = oldId) Then Continue For
                If o.IsDining Then Continue For
                Dim holder = Db.Query("SELECT TOP 1 s.[name] AS stall_name FROM (stalls AS s INNER JOIN users AS u ON s.owner_id = u.id) " &
                                      "WHERE s.slot_no = @n AND u.[status] = 'active'", Db.P("@n", o.Slot))
                Dim rows = RowsOf(holder)
                If rows.Count > 0 Then
                    err = "Slot " & o.Slot & " still has " & Db.GetStr(rows(0), "stall_name") & ". Move that stall to another slot first."
                    Return False
                End If
            Next

            For Each o In old.Slots
                Dim oldId = o.Id
                If lay.Slots.Any(Function(q) q.Id = oldId) Then Continue For
                Db.Exec("DELETE FROM floor_slots WHERE id = @id", Db.P("@id", o.Id))
                If Not o.IsDining Then Db.Exec("UPDATE stalls SET slot_no = NULL WHERE slot_no = @n", Db.P("@n", o.Slot))
            Next

            Dim maxSlot As Integer = 0
            For Each q In lay.Slots
                If q.Slot > maxSlot Then maxSlot = q.Slot
                If q.Id > 0 Then
                    Db.Exec("UPDATE floor_slots SET pos_x = @x, pos_y = @y, size_w = @w, size_h = @h, face = @f WHERE id = @id",
                            Db.P("@x", q.X), Db.P("@y", q.Y), Db.P("@w", q.W), Db.P("@h", q.H), Db.P("@f", q.Face), Db.P("@id", q.Id))
                Else
                    InsertShape(q.Slot, q.Kind, q.X, q.Y, q.W, q.H, q.Face)
                End If
            Next

            WriteSetting("floor_width", lay.Width.ToString())
            WriteSetting("floor_height", lay.Height.ToString())
            WriteSetting("floor_next_slot", Math.Max(lay.NextSlot, Math.Max(old.NextSlot, maxSlot + 1)).ToString())
            Return True
        Catch ex As Exception
            err = ex.Message
            Return False
        End Try
    End Function

    ' ---------------- stalls ----------------

    ''' <summary>All stalls of ACTIVE sellers with this month's rent status. Slot = 0 when not placed.</summary>
    Public Function GetStalls() As List(Of FloorStall)
        EnsureSchema()
        Dim result As New List(Of FloorStall)
        Dim today = Date.Today
        Dim monthStart As New Date(today.Year, today.Month, 1)

        Dim monthly As Decimal = 0D
        Dim txt = ReadSetting("monthly_rent", "0")
        If Not Decimal.TryParse(txt, NumberStyles.Any, CultureInfo.InvariantCulture, monthly) Then
            Decimal.TryParse(txt, monthly)
        End If

        Dim paidBy As New Dictionary(Of Integer, Decimal)
        Try
            For Each r In RowsOf(Db.Query("SELECT stall_id, SUM(amount) AS paid_sum FROM rent_payments WHERE rent_month = @m GROUP BY stall_id", Db.P("@m", monthStart)))
                paidBy(Db.GetInt(r, "stall_id")) = Db.GetDec(r, "paid_sum")
            Next
        Catch
            ' rent table missing: everything shows as unpaid
        End Try

        Dim sql = "SELECT s.id, s.[name] AS stall_name, s.slot_no, s.is_open, s.logo_path, s.rent_due_date, " &
                  "c.[name] AS cat_name, u.username, u.created_at " &
                  "FROM ((stalls AS s INNER JOIN users AS u ON s.owner_id = u.id) " &
                  "LEFT JOIN stall_categories AS c ON s.category_id = c.id) " &
                  "WHERE u.[status] = 'active' ORDER BY s.id"
        For Each r In RowsOf(Db.Query(sql))
            Dim st As New FloorStall()
            st.StallId = Db.GetInt(r, "id")
            st.Slot = Db.GetInt(r, "slot_no")
            st.Name = Db.GetStr(r, "stall_name")
            st.Category = Db.GetStr(r, "cat_name")
            st.Seller = Db.GetStr(r, "username")
            st.IsOpen = Db.GetBool(r, "is_open")
            st.LogoPath = Db.GetStr(r, "logo_path")

            Dim created = Db.GetDate(r, "created_at")
            If created.Year < 2000 Then created = today
            Dim startMonth As New Date(created.Year, created.Month, 1)
            Dim firstDue = Db.GetDate(r, "rent_due_date")
            If firstDue.Year < 2000 Then firstDue = created.AddMonths(1)
            Dim n = (monthStart.Year - startMonth.Year) * 12 + monthStart.Month - startMonth.Month
            If n < 0 Then n = 0
            Dim due = firstDue.AddMonths(n)

            Dim paid As Decimal = 0D
            paidBy.TryGetValue(st.StallId, paid)
            st.Rent = monthly
            st.Paid = paid
            st.Balance = Math.Max(0D, monthly - paid)
            st.DueDate = due
            If st.Balance <= 0D Then
                st.RentStatus = "paid"
            ElseIf due.Date < today Then
                st.RentStatus = "overdue"
                st.DaysLate = (today - due.Date).Days
            ElseIf paid > 0D Then
                st.RentStatus = "partial"
            Else
                st.RentStatus = "unpaid"
            End If
            result.Add(st)
        Next
        Return result
    End Function

    Public Function RunningOrders(stallId As Integer) As Integer
        Try
            Return ToInt(Db.Scalar("SELECT COUNT(*) FROM order_stalls WHERE stall_id = @s AND [status] IN ('pending','preparing','ready')", Db.P("@s", stallId)))
        Catch
            Return 0
        End Try
    End Function

    Public Function SetOpen(stallId As Integer, isOpen As Boolean, ByRef err As String) As Boolean
        err = ""
        Try
            Db.Exec("UPDATE stalls SET is_open = @o WHERE id = @id", Db.P("@o", If(isOpen, 1, 0)), Db.P("@id", stallId))
            Return True
        Catch ex As Exception
            err = ex.Message
            Return False
        End Try
    End Function

    ''' <summary>Puts a stall in a vacant slot (also used to move it). A slot only held by a deactivated seller's stall counts as vacant.</summary>
    Public Function AssignStall(stallId As Integer, slotNo As Integer, ByRef err As String) As Boolean
        err = ""
        Try
            EnsureSchema()
            If ToInt(Db.Scalar("SELECT COUNT(*) FROM floor_slots WHERE slot_no = @n AND kind = 'stall'", Db.P("@n", slotNo))) = 0 Then
                err = "That slot no longer exists."
                Return False
            End If
            Db.Exec("UPDATE stalls SET slot_no = NULL WHERE slot_no = @n AND owner_id IN (SELECT id FROM users WHERE [status] <> 'active')", Db.P("@n", slotNo))
            If ToInt(Db.Scalar("SELECT COUNT(*) FROM stalls WHERE slot_no = @n AND id <> @s", Db.P("@n", slotNo), Db.P("@s", stallId))) > 0 Then
                err = "Another stall already uses that slot."
                Return False
            End If
            Db.Exec("UPDATE stalls SET slot_no = @n WHERE id = @s", Db.P("@n", slotNo), Db.P("@s", stallId))
            Return True
        Catch ex As Exception
            err = ex.Message
            Return False
        End Try
    End Function
End Module
