using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TaekwondoAPI.Contracts;
using TaekwondoAPI.Data;
using TaekwondoAPI.Models;

namespace TaekwondoAPI.Services.TaekwonBot;

public sealed class BotService(TaekwondoDbContext db, StudentReferenceStore references, TimeProvider clock,
    ITaekwonBotProvider provider, IConfiguration config, OpenAiConversationEngine? conversation = null, ConversationStateStore? conversationStates = null)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static readonly SemaphoreSlim MutationLock = new(1, 1);
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private DateTime BusinessToday => TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires")).Date;
    private int RetentionDays => Math.Clamp(config.GetValue("TaekwonBot:HistoryRetentionDays", 30), 1, 3650);
    private sealed record PaymentSnapshot(int StudentId, string StudentName, int Year, int Month, int Amount, string Method, DateTime Date, string Status, int Version, string Class);
    private StudentChoice Choice(AlumnoClub student, string owner, string session) => new(references.Create(owner, session, "student", student.Id), student.NombreApellido, student.Graduacion, student.Horario, student.FechaRegistro);
    private PaymentSnapshot Snapshot(Cuota payment) => new(payment.AlumnoClubId ?? throw new BotException(409, "unlinked_payment", "Primero hay que relacionar inequívocamente el pago y su período."), payment.NombreApellido, payment.PeriodoAnio, payment.PeriodoMes, payment.Monto, payment.MedioPago, payment.Fecha, payment.Estado, payment.Version, payment.Clase);
    private PaymentView View(PaymentSnapshot payment, int? id, string owner, string session) => new(id.HasValue ? references.Create(owner, session, "payment", id.Value) : "", references.Create(owner, session, "student", payment.StudentId), payment.StudentName, payment.Year, payment.Month, payment.Amount, payment.Method, payment.Date, payment.Status, payment.Version);
    private OperationView View(BotOperation operation)
    {
        var before = JsonSerializer.Deserialize<PaymentSnapshot>(operation.BeforeJson, Json);
        var after = JsonSerializer.Deserialize<PaymentSnapshot>(operation.AfterJson, Json);
        return new(operation.Id, operation.Kind, operation.Status, operation.Kind switch { "create" => "Registrar pago", "edit" => "Corregir pago", "void" => "Dejar pago sin efecto", _ => "Restaurar pago" }, operation.ExpiresAt,
            before is null ? null : View(before, operation.PaymentId, operation.Owner, operation.Session),
            after is null ? null : View(after, operation.PaymentId, operation.Owner, operation.Session));
    }
    public async Task<IReadOnlyList<StudentChoice>> Students(string? query, string owner, string session)
    {
        if (query?.Length > 150) throw new BotException(400, "invalid_query", "La búsqueda es demasiado larga.");
        var students = await db.AlumnosClub.AsNoTracking().OrderBy(a => a.NombreApellido).ToListAsync();
        if (!string.IsNullOrWhiteSpace(query)) students = students.Where(a => a.NombreApellido.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        return students.Take(100).Select(a => Choice(a, owner, session)).ToArray();
    }
    public async Task<StudentChoice> LegacyStudent(int id, string owner, string session) => Choice(await db.AlumnosClub.FindAsync(id) ?? throw new BotException(404, "student_missing", "Alumno no encontrado."), owner, session);
    public async Task<PaymentView> LegacyPayment(int id, string owner, string session)
    {
        var payment = await db.Cuotas.FindAsync(id) ?? throw new BotException(404, "payment_missing", "Pago no encontrado.");
        return View(Snapshot(payment), payment.Id, owner, session);
    }
    public async Task<IReadOnlyList<PaymentView>> Payments(string? studentRef, string owner, string session)
    {
        var studentId = references.Resolve(studentRef, owner, session, "student");
        return (await db.Cuotas.AsNoTracking().Where(p => p.AlumnoClubId == studentId).OrderByDescending(p => p.Fecha).ToListAsync())
            .Select(p => View(Snapshot(p), p.Id, owner, session)).ToArray();
    }
    public async Task<DebtResult> Debt(string? studentRef, string owner, string session)
    {
        var studentId = references.Resolve(studentRef, owner, session, "student");
        var student = await db.AlumnosClub.FindAsync(studentId) ?? throw new BotException(404, "student_missing", "Alumno no encontrado.");
        if (student.FechaRegistro.Year < 2000) throw new BotException(409, "registration_date_missing", "Hay que confirmar la fecha de alta del alumno.");
        var today = BusinessToday;
        var start = new DateTime(student.FechaRegistro.Year, student.FechaRegistro.Month, 1);
        var finish = new DateTime(today.Year, today.Month, 1);
        var paid = (await db.Cuotas.AsNoTracking().Where(p => p.AlumnoClubId == studentId && p.Estado == "active").Select(p => new { p.PeriodoAnio, p.PeriodoMes }).ToListAsync()).Select(p => (p.PeriodoAnio, p.PeriodoMes)).ToHashSet();
        var missing = new List<Period>();
        for (var current = start; current <= finish; current = current.AddMonths(1)) if (!paid.Contains((current.Year, current.Month))) missing.Add(new(current.Year, current.Month));
        return new(student.NombreApellido, start.Year, start.Month, finish.Year, finish.Month, missing);
    }
    public async Task<StatisticsResult> Statistics(int year, int month)
    {
        ValidatePeriod(year, month);
        var end = new DateTime(year, month, 1).AddMonths(1);
        var students = await db.AlumnosClub.AsNoTracking().Where(a => a.FechaRegistro < end).Select(a => a.Id).ToListAsync();
        var payments = await db.Cuotas.AsNoTracking().Where(p => p.Estado == "active" && p.PeriodoAnio == year && p.PeriodoMes == month).ToListAsync();
        return new(year, month, students.Count, payments.Count, payments.Sum(p => p.Monto), students.Count(id => !payments.Any(p => p.AlumnoClubId == id)));
    }
    private static void ValidatePeriod(int year, int month)
    {
        if (year is < 2000 or > 2100 || month is < 1 or > 12) throw new BotException(400, "invalid_period", "Indicá año y mes válidos explícitamente.");
    }
    public async Task<OperationView> Prepare(PreparePaymentRequest request, string owner, string session)
    {
        if (request.Kind is not ("create" or "edit" or "void" or "restore")) throw new BotException(400, "invalid_kind", "Operación no autorizada.");
        if (request.Kind != "create" && (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 500)) throw new BotException(400, "reason_required", "Indicá un motivo de hasta 500 caracteres.");
        Cuota? existing = null;
        if (request.Kind != "create") existing = await db.Cuotas.FindAsync(references.Resolve(request.PaymentRef, owner, session, "payment")) ?? throw new BotException(404, "payment_missing", "Pago no encontrado.");
        var before = existing is null ? null : Snapshot(existing);
        PaymentSnapshot after;
        if (request.Kind is "void" or "restore")
        {
            if (before!.Status != (request.Kind == "void" ? "active" : "voided")) throw new BotException(409, "invalid_state", "El pago ya no tiene el estado esperado.");
            after = before with { Status = request.Kind == "void" ? "voided" : "active", Version = before.Version + 1 };
        }
        else
        {
            if (existing is not null && existing.Estado != "active") throw new BotException(409, "invalid_state", "Restaurá el pago antes de editarlo.");
            var studentId = references.Resolve(request.StudentRef, owner, session, "student");
            var student = await db.AlumnosClub.FindAsync(studentId) ?? throw new BotException(404, "student_missing", "Alumno no encontrado.");
            if (!request.Year.HasValue || !request.Month.HasValue || !request.Amount.HasValue || !request.Date.HasValue || string.IsNullOrWhiteSpace(request.Method)) throw new BotException(400, "missing_values", "Faltan año, mes, importe, medio de pago o fecha explícitos.");
            ValidatePeriod(request.Year.Value, request.Month.Value);
            if (request.Method is not ("Efectivo" or "Transferencia" or "Beca") || request.Amount < 0 || request.Amount > 100_000_000 || (request.Method != "Beca" && request.Amount == 0) || (request.Method == "Beca" && request.Amount != 0)) throw new BotException(400, "invalid_amount", "Revisá el importe y el medio de pago. Beca requiere importe cero.");
            if (request.Date.Value.Year is < 2000 or > 2100) throw new BotException(400, "invalid_date", "Indicá una fecha válida.");
            if (student.FechaRegistro.Year < 2000) throw new BotException(409, "registration_date_missing", "Hay que confirmar la fecha de alta del alumno.");
            if (new DateTime(request.Year.Value, request.Month.Value, 1) < new DateTime(student.FechaRegistro.Year, student.FechaRegistro.Month, 1)) throw new BotException(400, "period_before_registration", "El período es anterior al alta del alumno.");
            after = new(student.Id, student.NombreApellido, request.Year.Value, request.Month.Value, request.Amount.Value, request.Method, request.Date.Value.ToUniversalTime(), "active", (existing?.Version ?? 0) + 1, student.Horario);
        }
        await RejectDuplicate(after, existing?.Id);
        var operation = new BotOperation { Id = Guid.NewGuid().ToString("N"), Owner = owner, Session = session, Kind = request.Kind, PaymentId = existing?.Id, ExpectedVersion = existing?.Version ?? 0, BeforeJson = JsonSerializer.Serialize(before, Json), AfterJson = JsonSerializer.Serialize(after, Json), Reason = request.Reason?.Trim() ?? "", CreatedAt = Now, ExpiresAt = Now.AddMinutes(5) };
        db.BotOperations.Add(operation);
        await db.SaveChangesAsync();
        return View(operation);
    }
    private async Task RejectDuplicate(PaymentSnapshot after, int? excludedId)
    {
        if (after.Status == "active" && await db.Cuotas.AnyAsync(p => p.Id != excludedId && p.AlumnoClubId == after.StudentId && p.PeriodoAnio == after.Year && p.PeriodoMes == after.Month && p.Estado == "active")) throw new BotException(409, "duplicate_payment", "Ya existe un pago activo para ese alumno y período. Corregí el existente.");
    }
    public async Task<OperationView> Operation(string reference, string owner, string session, string action = "status")
    {
        await MutationLock.WaitAsync();
        try
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync();
            var operation = await db.BotOperations.FindAsync(reference);
            if (operation is null || operation.Owner != owner || operation.Session != session) throw new BotException(404, "operation_missing", "Operación no encontrada en esta sesión.");
            if (operation.Status == "prepared" && operation.ExpiresAt <= Now) operation.Status = "expired";
            if (action == "confirm" && operation.Status == "prepared")
            {
                var after = JsonSerializer.Deserialize<PaymentSnapshot>(operation.AfterJson, Json)!;
                Cuota payment;
                if (operation.Kind == "create") payment = new Cuota();
                else
                {
                    payment = await db.Cuotas.FindAsync(operation.PaymentId) ?? throw new BotException(409, "payment_changed", "El pago ya no está disponible.");
                    if (payment.Version != operation.ExpectedVersion) throw new BotException(409, "stale_payment", "Otro cambio modificó el pago. Prepará una nueva corrección.");
                }
                var student = await db.AlumnosClub.AsNoTracking().FirstOrDefaultAsync(s => s.Id == after.StudentId);
                if (student is null || student.NombreApellido != after.StudentName || student.Horario != after.Class || student.FechaRegistro.Year < 2000 || new DateTime(after.Year, after.Month, 1) < new DateTime(student.FechaRegistro.Year, student.FechaRegistro.Month, 1)) throw new BotException(409, "student_changed", "Los datos del alumno cambiaron. Revisá una nueva propuesta.");
                await RejectDuplicate(after, operation.PaymentId);
                payment.AlumnoClubId = after.StudentId; payment.NombreApellido = after.StudentName; payment.PeriodoAnio = after.Year; payment.PeriodoMes = after.Month;
                payment.Mes = new CultureInfo("es-AR").DateTimeFormat.GetMonthName(after.Month); payment.Monto = after.Amount; payment.MedioPago = after.Method;
                payment.Fecha = after.Date; payment.Estado = after.Status; payment.Version = after.Version; payment.Clase = after.Class;
                if (operation.Kind == "create") db.Cuotas.Add(payment);
                await db.SaveChangesAsync();
                operation.PaymentId = payment.Id; operation.Status = "confirmed";
                db.PaymentAuditEvents.Add(new PaymentAuditEvent { OperationId = operation.Id, PaymentId = payment.Id, Owner = owner, Kind = operation.Kind, Reason = operation.Reason, BeforeJson = operation.BeforeJson, AfterJson = operation.AfterJson, CreatedAt = Now });
            }
            else if (action == "cancel" && operation.Status == "prepared") operation.Status = "cancelled";
            else if (action == "confirm" && operation.Status is "cancelled" or "expired") throw new BotException(410, "operation_closed", "La propuesta fue cancelada o venció.");
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return View(operation);
        }
        catch (DbUpdateConcurrencyException) { throw new BotException(409, "stale_payment", "El pago cambió. Consultá el estado antes de reintentar."); }
        catch (DbUpdateException) { throw new BotException(409, "payment_conflict", "La operación entró en conflicto. Consultá su estado antes de reintentar."); }
        finally { MutationLock.Release(); }
    }
    public async Task<HistoryPage> History(string owner)
    {
        var cutoff = Now.AddDays(-RetentionDays);
        await db.BotHistoryMessages.Where(m => m.CreatedAt < cutoff).ExecuteDeleteAsync();
        var messages = await db.BotHistoryMessages.AsNoTracking().Where(m => m.Owner == owner).OrderByDescending(m => m.Id).Take(200).OrderBy(m => m.Id).Select(m => new HistoryMessage(m.Id, m.Role, m.Text, m.CreatedAt)).ToListAsync();
        return new(messages, RetentionDays);
    }
    public Task<int> DeleteHistory(string owner)
    {
        conversationStates?.Delete(owner);
        return db.BotHistoryMessages.Where(m => m.Owner == owner).ExecuteDeleteAsync();
    }
    public async Task<ChatResponse> Chat(ChatRequest request, string owner, string session, CancellationToken cancellationToken)
    {
        if (request.Text is null || request.Text.Length > 2000) throw new BotException(400, "invalid_text", "El mensaje debe tener hasta 2000 caracteres.");
        if (provider.Name == "openai" && conversation is not null && conversationStates is not null)
            return await ChatConversational(request.Text, owner, session, cancellationToken);
        if (provider.Name == "mock" && conversationStates is not null && request.MockScenario is null)
            return await ChatMockConversational(request.Text, owner, session, cancellationToken);
        var intent = request.Intent ?? InferIntent(request.Text);
        ChatResponse response;
        if (intent is not ("students" or "payments" or "debts" or "statistics" or "help")) response = new("clarification", "Elegí una acción y seleccioná al alumno con el buscador local. El texto libre permanece aquí y no se envía al proveedor.", provider.Name);
        else if ((intent is "payments" or "debts") && request.StudentRef is null) response = new("clarification", "¿Qué alumno querés consultar? Seleccionalo en el buscador local.", provider.Name, Students: await Students(null, owner, session));
        else if (intent == "statistics" && (!request.Year.HasValue || !request.Month.HasValue)) response = new("clarification", "¿Qué año y mes querés consultar?", provider.Name);
        else
        {
            if (request.MockScenario is not null && provider.Name != "mock") throw new BotException(400, "mock_only", "Este escenario solo está disponible con mock.");
            var payload = ProviderPayloadFactory.Create(intent, request.MockScenario, request.Year, request.Month);
            try
            {
                var result = await provider.RespondAsync(payload, cancellationToken);
                response = request.MockScenario switch
                {
                    "no_students" => new("answer", "No se encontraron alumnos para este escenario ficticio.", provider.Name, Students: []),
                    "ambiguous" => new("clarification", "Hay dos coincidencias. Elegí un alumno explícitamente.", provider.Name, Students: (await Students("Alex Prueba", owner, session)).Take(2).ToArray()),
                    "duplicate" => new("error", "Ya existe un pago activo para ese alumno y período. Revisá el existente.", provider.Name),
                    _ => await AnswerWithLocalData(intent, request, owner, session, result.Message)
                };
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { response = new("error", "El proveedor demoró demasiado. No se ejecutó ningún cambio.", provider.Name); }
            catch (TimeoutException) { response = new("error", "El proveedor demoró demasiado. No se ejecutó ningún cambio.", provider.Name); }
            catch (OpenAiProviderException ex) { response = new("error", ex.Message, provider.Name); }
            catch (Exception ex) when (ex is not BotException and not OperationCanceledException) { response = new("error", "El proveedor no está disponible. Los datos locales siguen disponibles.", provider.Name); }
        }
        // El historial local no guarda texto original ni credenciales.
        db.BotHistoryMessages.Add(new() { Owner = owner, Role = "user", Text = intent is "students" or "payments" or "debts" or "statistics" or "help" ? $"Consulta: {intent}" : "Consulta pendiente de selección local", CreatedAt = Now });
        db.BotHistoryMessages.Add(new() { Owner = owner, Role = "assistant", Text = response.Message, CreatedAt = Now });
        await db.SaveChangesAsync(cancellationToken);
        return response;
    }

    private async Task<ChatResponse> ChatConversational(string text, string owner, string session, CancellationToken cancellationToken)
    {
        if (Regex.IsMatch(text, @"(?i)(?:sk-[A-Za-z0-9_-]{10,}|Bearer\s+\S+|(?:password|contraseña|api[_ -]?key|clave\s+de\s+api)\s*[:=]\s*\S+)", RegexOptions.CultureInvariant))
            return new("error", "El mensaje parece contener una clave o contraseña. Quitala antes de continuar.", "openai");
        var state = conversationStates!.Get(owner, session);
        await state.Gate.WaitAsync(cancellationToken);
        try
        {
            var requestedChange = state.PendingOperationRef is not null && TryReadAmountCorrection(text, out _)
                ? null : ReadChangeKind(text);
            if (requestedChange is not null)
            {
                state.ChangeKind = requestedChange;
                state.ChangeReason = null;
                state.Year = state.Month = state.Amount = null;
                state.Method = null;
                state.Candidates = [];
                state.SelectedStudentRef = null;
                state.ReadIntent = null;
            }
            else if (Regex.IsMatch(text, @"\b(cargale|cargar|registrar)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                state.ChangeKind = null;
            var reason = ReadChangeReason(text);
            if (state.ChangeKind is not null && reason is not null) state.ChangeReason = reason;
            CapturePaymentDetails(text, state);
            var readIntent = InferIntent(text);
            if (requestedChange is null && (readIntent == "debts" || readIntent == "statistics" || readIntent == "payments"))
            { state.ChangeKind = null; state.ChangeReason = null; }
            if (Regex.IsMatch(text, @"\b(cargale|cargar|registrar|anular|corregir|editar|restaurar)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) state.ReadIntent = null;
            else if (readIntent is "debts" or "statistics" or "payments") state.ReadIntent = readIntent;
            if (state.ReadIntent == "statistics" && Regex.IsMatch(text, @"\b(este mes|mes actual)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            { state.Year = BusinessToday.Year; state.Month = BusinessToday.Month; }
            ChatResponse response;
            if (state.PendingOperationRef is not null && Regex.IsMatch(text.Trim(), @"^(sí|si|confirmo|confirmar|dale|ok|acepto)([,!.\s]+(confirmo|la|el|operación|operacion|propuesta|pago))*[.!\s]*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                var completed = await Operation(state.PendingOperationRef, owner, session, "confirm");
                state.PendingOperationRef = null;
                state.ChangeKind = null;
                response = new("answer", completed.Kind == "create"
                    ? $"Registré el pago de {completed.After?.StudentName} para {completed.After?.Month:00}/{completed.After?.Year} por ${completed.After?.Amount:N0}."
                    : $"Confirmé la operación «{completed.Summary}» para {completed.After?.StudentName ?? completed.Before?.StudentName}.", "openai", Operation: completed);
            }
            else if (state.PendingOperationRef is not null && Regex.IsMatch(text.Trim(), @"^(cancel[aá]|cancelar|no)([,!.\s]+(la|el|operación|operacion|propuesta|pago))*[.!\s]*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                await Operation(state.PendingOperationRef, owner, session, "cancel");
                state.PendingOperationRef = null;
                state.ChangeKind = null;
                response = new("answer", "Cancelé la propuesta. No se modificó ningún pago.", "openai");
            }
            else if (state.PendingOperationRef is not null && TryReadAmountCorrection(text, out var correctedAmount)
                && state.SelectedStudentRef is not null && state.Year.HasValue && state.Month.HasValue && state.Method is not null)
            {
                var previous = state.PendingOperationRef;
                var replacement = await Prepare(new PreparePaymentRequest("create", state.SelectedStudentRef,
                    Year: state.Year, Month: state.Month, Amount: correctedAmount, Method: state.Method, Date: BusinessToday), owner, session);
                await Operation(previous, owner, session, "cancel");
                state.PendingOperationRef = replacement.OperationRef;
                state.Amount = correctedAmount;
                response = new("confirmation", $"Corregí la propuesta: pago de {replacement.After!.StudentName} para {replacement.After.Month:00}/{replacement.After.Year} por ${replacement.After.Amount:N0} mediante {replacement.After.Method}. ¿Confirmás que lo registre?", "openai", Operation: replacement, PendingOperation: replacement, ConfirmationRequired: true);
            }
            else
            {
                OperationView? prepared = null;
                DebtResult? debtResult = null;
                IReadOnlyList<PaymentView>? paymentsResult = null;
                StatisticsResult? statisticsResult = null;
                var message = await conversation!.RunAsync(text, owner, state, BusinessToday, async (name, args) =>
                {
                    if (name == "search_students")
                    {
                        var query = args.GetProperty("query").GetString()?.Trim() ?? "";
                        if (query.Length is < 2 or > 100) return new { error = "Indicá el nombre del alumno para buscar." };
                        var candidates = (await Students(query, owner, session)).Take(10).ToList();
                        state.Candidates = candidates;
                        state.SelectedStudentRef = candidates.Count == 1 ? candidates[0].StudentRef : null;
                        if (state.ChangeKind is not null && candidates.Count == 1)
                        {
                            prepared = await PrepareExistingFromState(state, owner, session);
                            if (prepared is not null)
                            {
                                state.PendingOperationRef = prepared.OperationRef;
                                return new { prepared = true, requiresConfirmation = true, kind = state.ChangeKind };
                            }
                            return new { selected = true, missing = MissingChangeDetail(state) };
                        }
                        if (state.ReadIntent == "debts" && candidates.Count == 1)
                        {
                            debtResult = await Debt(candidates[0].StudentRef, owner, session);
                            return new { ready = true, pendingCount = debtResult.PendingMonths.Count, pendingMonths = debtResult.PendingMonths.Take(36) };
                        }
                        if (state.ReadIntent == "payments" && candidates.Count == 1)
                        {
                            paymentsResult = await Payments(candidates[0].StudentRef, owner, session);
                            return new { ready = true, paymentCount = paymentsResult.Count,
                                payments = paymentsResult.Take(20).Select(payment => new { payment.Year, payment.Month, payment.Amount, payment.Method, payment.Status }) };
                        }
                        return new { students = candidates.Select((c, index) => new { position = index + 1, c.StudentRef, c.DisplayName, c.Graduacion, c.Horario, registrationMonth = c.RegisteredAt.Month, registrationYear = c.RegisteredAt.Year }), ambiguous = candidates.Count > 1 };
                    }
                    if (name == "get_debt")
                    {
                        var studentRef = args.GetProperty("studentRef").GetString();
                        if (!state.Candidates.Any(c => c.StudentRef == studentRef) || state.Candidates.Count > 1 && state.SelectedStudentRef != studentRef)
                            return new { error = "Elegí el alumno antes de consultar sus meses pendientes." };
                        debtResult = await Debt(studentRef, owner, session);
                        return new { ready = true, pendingCount = debtResult.PendingMonths.Count, pendingMonths = debtResult.PendingMonths.Take(36) };
                    }
                    if (name == "get_payments")
                    {
                        var studentRef = args.GetProperty("studentRef").GetString();
                        if (!state.Candidates.Any(c => c.StudentRef == studentRef) || state.Candidates.Count > 1 && state.SelectedStudentRef != studentRef)
                            return new { error = "Elegí el alumno antes de consultar sus pagos." };
                        paymentsResult = await Payments(studentRef, owner, session);
                        return new { ready = true, paymentCount = paymentsResult.Count,
                            payments = paymentsResult.Take(20).Select(payment => new { payment.Year, payment.Month, payment.Amount, payment.Method, payment.Status }) };
                    }
                    if (name == "get_statistics")
                    {
                        var year = args.GetProperty("year").GetInt32();
                        var month = args.GetProperty("month").GetInt32();
                        if (state.Year != year || state.Month != month)
                            return new { error = "Indicá año y mes explícitos o pedí el mes actual." };
                        statisticsResult = await Statistics(year, month);
                        return new { ready = true, statisticsResult.Year, statisticsResult.Month, statisticsResult.Students,
                            statisticsResult.Payments, statisticsResult.TotalAmount, statisticsResult.PendingStudents };
                    }
                    if (name == "get_year_revenue")
                    {
                        var year = args.GetProperty("year").GetInt32();
                        var explicitYear = Regex.Match(text, @"\b20\d{2}\b", RegexOptions.CultureInvariant);
                        var expectedYear = explicitYear.Success ? int.Parse(explicitYear.Value, CultureInfo.InvariantCulture)
                            : Regex.IsMatch(text, @"a[nñ]o pasado|a[nñ]o anterior", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                                ? BusinessToday.Year - 1 : BusinessToday.Year;
                        if (year != expectedYear) return new { error = "El año elegido no coincide con la solicitud." };
                        var payments = await db.Cuotas.AsNoTracking()
                            .Where(payment => payment.Estado == "active" && payment.PeriodoAnio == year)
                            .Select(payment => new { payment.PeriodoMes, payment.Monto }).ToListAsync(cancellationToken);
                        var months = Enumerable.Range(1, 12).Select(month => new
                        {
                            month,
                            totalAmount = payments.Where(payment => payment.PeriodoMes == month).Sum(payment => payment.Monto),
                            paymentCount = payments.Count(payment => payment.PeriodoMes == month)
                        }).ToArray();
                        return new { year, currency = "ARS", months };
                    }
                    if (name == "prepare_payment")
                    {
                        if (state.ChangeKind is not null) return new { error = "Para corregir o anular un pago, primero identificá el pago existente y el motivo." };
                        if (prepared is not null)
                            return new { prepared = true, operationRef = prepared.OperationRef, requiresConfirmation = true };
                        var studentRef = args.GetProperty("studentRef").GetString() ?? "";
                        if (!state.Candidates.Any(c => c.StudentRef == studentRef)) return new { error = "Seleccioná un alumno de la búsqueda antes de preparar el pago." };
                        var year = args.GetProperty("year").GetInt32();
                        var month = args.GetProperty("month").GetInt32();
                        var amount = args.GetProperty("amount").GetInt32();
                        var method = NormalizeMethod(args.GetProperty("method").GetString());
                        if (!state.Year.HasValue || !state.Month.HasValue || !state.Amount.HasValue || state.Method is null)
                            return new { error = "Faltan año, mes, importe o medio de pago explícitos. Preguntá el dato faltante." };
                        if (year != state.Year || month != state.Month || amount != state.Amount || method != state.Method)
                            return new { error = "Los datos propuestos no coinciden con lo indicado por el usuario." };
                        if (method is null) return new { error = "Medio de pago inválido. Usá efectivo, transferencia o beca." };
                        if (state.Candidates.Count > 1 && state.SelectedStudentRef is null) return new { error = "Hay alumnos homónimos. Pedí al usuario que elija uno antes de preparar." };
                        if (state.SelectedStudentRef is not null && state.SelectedStudentRef != studentRef) return new { error = "El alumno elegido no coincide con la conversación." };
                        var previousOperation = state.PendingOperationRef;
                        prepared = await Prepare(new PreparePaymentRequest("create", studentRef, Year: year, Month: month, Amount: amount, Method: method, Date: BusinessToday), owner, session);
                        if (previousOperation is not null)
                            await Operation(previousOperation, owner, session, "cancel");
                        state.PendingOperationRef = prepared.OperationRef;
                        state.SelectedStudentRef = studentRef;
                        if (state.ChangeKind is not null)
                        {
                            prepared = await PrepareExistingFromState(state, owner, session);
                            if (prepared is not null)
                            {
                                state.PendingOperationRef = prepared.OperationRef;
                                return new { selected = true, prepared = true, requiresConfirmation = true, kind = state.ChangeKind };
                            }
                            return new { selected = true, missing = MissingChangeDetail(state) };
                        }
                        state.Year = year; state.Month = month; state.Amount = amount; state.Method = method;
                        return new { prepared = true, operationRef = prepared.OperationRef, studentName = prepared.After?.StudentName, year, month, amount, method, requiresConfirmation = true };
                    }
                    if (name == "select_student")
                    {
                        var studentRef = args.GetProperty("studentRef").GetString();
                        if (state.Candidates.Count < 2 || !state.Candidates.Any(c => c.StudentRef == studentRef))
                            return new { error = "Elegí uno de los alumnos mostrados en esta conversación." };
                        state.SelectedStudentRef = studentRef;
                        if (state.ReadIntent == "debts")
                        {
                            debtResult = await Debt(studentRef, owner, session);
                            return new { selected = true, ready = true, pendingCount = debtResult.PendingMonths.Count,
                                pendingMonths = debtResult.PendingMonths.Take(36) };
                        }
                        if (state.ReadIntent == "payments")
                        {
                            paymentsResult = await Payments(studentRef, owner, session);
                            return new { selected = true, ready = true, paymentCount = paymentsResult.Count,
                                payments = paymentsResult.Take(20).Select(payment => new { payment.Year, payment.Month, payment.Amount, payment.Method, payment.Status }) };
                        }
                        if (state.Year.HasValue && state.Month.HasValue && state.Amount.HasValue && state.Method is not null)
                        {
                            prepared = await Prepare(new PreparePaymentRequest("create", studentRef,
                                Year: state.Year, Month: state.Month, Amount: state.Amount, Method: state.Method, Date: BusinessToday), owner, session);
                            state.PendingOperationRef = prepared.OperationRef;
                            return new { selected = true, prepared = true, operationRef = prepared.OperationRef,
                                studentName = prepared.After?.StudentName, state.Year, state.Month, state.Amount, state.Method,
                                requiresConfirmation = true };
                        }
                        return new { selected = true, studentRef, studentName = state.Candidates.Single(c => c.StudentRef == studentRef).DisplayName,
                            state.Year, state.Month, state.Amount, state.Method };
                    }
                    return new { error = "Herramienta no permitida." };
                }, cancellationToken);
                if (prepared is not null)
                    response = new("confirmation", prepared.Kind == "create"
                        ? $"Preparé el pago de {prepared.After!.StudentName} para {prepared.After.Month:00}/{prepared.After.Year} por ${prepared.After.Amount:N0} mediante {prepared.After.Method}. ¿Confirmás que lo registre?"
                        : $"Preparé la operación «{prepared.Summary}» para {prepared.After?.StudentName ?? prepared.Before?.StudentName}. Revisá el antes y después. ¿Confirmás?", "openai", Operation: prepared, PendingOperation: prepared, ConfirmationRequired: true);
                else if (debtResult is not null)
                    response = new("answer", RejectFalseWriteClaim(message), "openai", Debt: debtResult);
                else if (paymentsResult is not null)
                    response = new("answer", RejectFalseWriteClaim(message), "openai", Payments: paymentsResult);
                else if (statisticsResult is not null)
                    response = new("answer", RejectFalseWriteClaim(message), "openai", Statistics: statisticsResult);
                else if (state.Candidates.Count > 1 && state.SelectedStudentRef is null)
                    response = new("clarification", message, "openai", Students: state.Candidates, StudentCandidates: state.Candidates);
                else response = new("answer", RejectFalseWriteClaim(message), "openai");
            }
            db.BotHistoryMessages.Add(new() { Owner = owner, Role = "user", Text = SanitizeHistoryText(text), CreatedAt = Now });
            db.BotHistoryMessages.Add(new() { Owner = owner, Role = "assistant", Text = response.Message, CreatedAt = Now });
            await db.SaveChangesAsync(cancellationToken);
            return response;
        }
        catch (OpenAiProviderException ex) { return new("error", ex.Message, "openai"); }
        finally { state.Gate.Release(); }
    }

    private async Task<ChatResponse> ChatMockConversational(string text, string owner, string session, CancellationToken cancellationToken)
    {
        var state = conversationStates!.Get(owner, session);
        await state.Gate.WaitAsync(cancellationToken);
        try
        {
            ChatResponse response;
            var normalized = text.ToLowerInvariant();
            if (state.PendingOperationRef is not null && Regex.IsMatch(normalized.Trim(), @"^(sí|si|confirmo|confirmar|dale|ok|acepto)[.!\s]*$", RegexOptions.CultureInvariant))
            {
                var completed = await Operation(state.PendingOperationRef, owner, session, "confirm");
                state.PendingOperationRef = null;
                state.ChangeKind = null;
                response = new("answer", completed.Kind == "create" ? $"Registré el pago de {completed.After?.StudentName} por ${completed.After?.Amount:N0}."
                    : $"Confirmé la operación «{completed.Summary}» para {completed.After?.StudentName ?? completed.Before?.StudentName}.", "mock", Operation: completed);
            }
            else if (state.PendingOperationRef is not null && Regex.IsMatch(normalized.Trim(), @"^(no|cancelar|cancela)[.!\s]*$", RegexOptions.CultureInvariant))
            {
                await Operation(state.PendingOperationRef, owner, session, "cancel");
                state.PendingOperationRef = null;
                state.ChangeKind = null;
                response = new("answer", "Cancelé la propuesta. No se modificó ningún pago.", "mock");
            }
            else if (state.PendingOperationRef is not null && TryReadAmountCorrection(text, out var corrected) && state.SelectedStudentRef is not null && state.Year.HasValue && state.Month.HasValue && state.Method is not null)
            {
                var old = state.PendingOperationRef;
                var replacement = await Prepare(new PreparePaymentRequest("create", state.SelectedStudentRef, Year: state.Year, Month: state.Month, Amount: corrected, Method: state.Method, Date: BusinessToday), owner, session);
                await Operation(old, owner, session, "cancel");
                state.PendingOperationRef = replacement.OperationRef;
                state.Amount = corrected;
                response = new("confirmation", $"Corregí la propuesta a ${corrected:N0}. ¿Confirmás que registre el pago?", "mock", Operation: replacement, PendingOperation: replacement, ConfirmationRequired: true);
            }
            else
            {
                var requestedChange = ReadChangeKind(text);
                if (requestedChange is not null)
                {
                    state.ChangeKind = requestedChange;
                    state.ChangeReason = null;
                    state.Year = state.Month = state.Amount = null;
                    state.Method = null;
                    state.Candidates = [];
                    state.SelectedStudentRef = null;
                    state.ReadIntent = "change";
                }
                else if (Regex.IsMatch(normalized, @"\b(cargale|cargar|registrar)\b", RegexOptions.CultureInvariant))
                    state.ChangeKind = null;
                var reason = ReadChangeReason(text);
                if (state.ChangeKind is not null && reason is not null) state.ChangeReason = reason;
                CapturePaymentDetails(text, state);
                var intent = InferIntent(text);
                if (requestedChange is null && (intent == "debts" || intent == "statistics" || intent == "payments"))
                { state.ChangeKind = null; state.ChangeReason = null; }
                if (state.ChangeKind is not null) state.ReadIntent = "change";
                else if (Regex.IsMatch(normalized, @"\b(cargale|cargar|registrar)\b", RegexOptions.CultureInvariant)) state.ReadIntent = "payment";
                else if (intent is "debts" or "statistics" or "payments") state.ReadIntent = intent;
                if (state.ReadIntent == "statistics" && Regex.IsMatch(normalized, @"\b(este mes|mes actual)\b", RegexOptions.CultureInvariant))
                { state.Year = BusinessToday.Year; state.Month = BusinessToday.Month; }
                if (state.ReadIntent == "statistics")
                {
                    if (!state.Year.HasValue || !state.Month.HasValue) response = new("clarification", "¿Qué año y mes querés consultar?", "mock");
                    else
                    {
                        var stats = await Statistics(state.Year.Value, state.Month.Value);
                        response = new("answer", $"Estas son las estadísticas de {stats.Month:00}/{stats.Year}.", "mock", Statistics: stats);
                    }
                }
                else
                {
                    if (state.Candidates.Count > 1 && Regex.IsMatch(normalized, @"\b(el segundo|la segunda|segundo|segunda|2)\b", RegexOptions.CultureInvariant))
                        state.SelectedStudentRef = state.Candidates[1].StudentRef;
                    else if (intent is "debts" or "payments" || state.ReadIntent is "payment" or "change" && state.Candidates.Count == 0)
                    {
                        var all = await Students(null, owner, session);
                        state.Candidates = all.Where(student => text.Contains(student.DisplayName.Split(' ')[0], StringComparison.OrdinalIgnoreCase)).Take(10).ToList();
                        state.SelectedStudentRef = state.Candidates.Count == 1 ? state.Candidates[0].StudentRef : null;
                    }
                    if (state.Candidates.Count == 0) response = new("clarification", "¿De qué alumno hablás? Indicá su nombre.", "mock");
                    else if (state.SelectedStudentRef is null) response = new("clarification", "Encontré varios alumnos con ese nombre. ¿Cuál elegís?", "mock", Students: state.Candidates, StudentCandidates: state.Candidates);
                    else if (state.ReadIntent == "debts")
                    {
                        var debt = await Debt(state.SelectedStudentRef, owner, session);
                        response = new("answer", $"{debt.StudentName} tiene {debt.PendingMonths.Count} meses pendientes.", "mock", Debt: debt);
                    }
                    else if (state.ReadIntent == "payments")
                    {
                        var payments = await Payments(state.SelectedStudentRef, owner, session);
                        response = new("answer", $"Encontré {payments.Count} pagos para el alumno seleccionado.", "mock", Payments: payments);
                    }
                    else if (state.ReadIntent == "change")
                    {
                        var prepared = await PrepareExistingFromState(state, owner, session);
                        if (prepared is null) response = new("clarification", MissingChangeDetail(state), "mock");
                        else
                        {
                            state.PendingOperationRef = prepared.OperationRef;
                            response = new("confirmation", $"Preparé «{prepared.Summary}» para {prepared.After?.StudentName ?? prepared.Before?.StudentName}. Revisá el antes y después. ¿Confirmás?", "mock",
                                Operation: prepared, PendingOperation: prepared, ConfirmationRequired: true);
                        }
                    }
                    else if (state.ReadIntent == "payment")
                    {
                        var missing = !state.Month.HasValue ? "mes" : !state.Year.HasValue ? "año" : !state.Amount.HasValue ? "importe" : state.Method is null ? "medio de pago" : null;
                        if (missing is not null) response = new("clarification", $"¿Cuál es el {missing} del pago?", "mock");
                        else
                        {
                            var prepared = await Prepare(new PreparePaymentRequest("create", state.SelectedStudentRef, Year: state.Year, Month: state.Month, Amount: state.Amount, Method: state.Method, Date: BusinessToday), owner, session);
                            state.PendingOperationRef = prepared.OperationRef;
                            response = new("confirmation", $"Preparé el pago de {prepared.After!.StudentName} por ${prepared.After.Amount:N0}. ¿Confirmás que lo registre?", "mock", Operation: prepared, PendingOperation: prepared, ConfirmationRequired: true);
                        }
                    }
                    else response = new("clarification", "¿Querés consultar meses pendientes o preparar un pago?", "mock");
                }
            }
            db.BotHistoryMessages.Add(new() { Owner = owner, Role = "user", Text = SanitizeHistoryText(text), CreatedAt = Now });
            db.BotHistoryMessages.Add(new() { Owner = owner, Role = "assistant", Text = response.Message, CreatedAt = Now });
            await db.SaveChangesAsync(cancellationToken);
            return response;
        }
        finally { state.Gate.Release(); }
    }

    private static string SanitizeHistoryText(string text)
    {
        var sanitized = Regex.Replace(text, @"[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}", "[correo oculto]", RegexOptions.CultureInvariant);
        sanitized = Regex.Replace(sanitized, @"\b\d{7,}\b", "[número oculto]", RegexOptions.CultureInvariant);
        return Regex.Replace(sanitized, @"\b(sk-[A-Za-z0-9_-]{10,}|Bearer\s+[A-Za-z0-9._-]{10,})\b", "[secreto oculto]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string? ReadChangeKind(string text)
    {
        var normalized = text.ToLowerInvariant().Replace('á', 'a').Replace('é', 'e').Replace('í', 'i').Replace('ó', 'o').Replace('ú', 'u');
        if (Regex.IsMatch(normalized, @"\b(anul\w*|dej\w*\s+sin\s+efecto)\b", RegexOptions.CultureInvariant)) return "void";
        if (Regex.IsMatch(normalized, @"\b(restaur\w*|reactiv\w*)\b", RegexOptions.CultureInvariant)) return "restore";
        if (Regex.IsMatch(normalized, @"\b(correg\w*|edit\w*|modific\w*)\b", RegexOptions.CultureInvariant)) return "edit";
        return null;
    }

    private static string? ReadChangeReason(string text)
    {
        var match = Regex.Match(text, @"\b(?:porque|por|motivo\s*:)\s+(.+?)[.!\s]*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static string MissingChangeDetail(ConversationState state) =>
        string.IsNullOrWhiteSpace(state.ChangeReason) ? "Indicá el motivo de la corrección o anulación." :
        !state.Year.HasValue || !state.Month.HasValue ? "Indicá el año y mes del pago existente." :
        state.ChangeKind == "edit" && !state.Amount.HasValue && state.Method is null ? "Indicá qué importe o medio de pago querés corregir." :
        "No encontré un único pago con ese alumno, período y estado. Revisá los pagos y precisá cuál querés cambiar.";

    private async Task<OperationView?> PrepareExistingFromState(ConversationState state, string owner, string session)
    {
        if (state.ChangeKind is null || state.SelectedStudentRef is null || !state.Year.HasValue || !state.Month.HasValue || string.IsNullOrWhiteSpace(state.ChangeReason)) return null;
        if (state.ChangeKind == "edit" && !state.Amount.HasValue && state.Method is null) return null;
        var payments = await Payments(state.SelectedStudentRef, owner, session);
        var expectedStatus = state.ChangeKind == "restore" ? "voided" : "active";
        var matches = payments.Where(payment => payment.Year == state.Year && payment.Month == state.Month && payment.Status == expectedStatus).ToArray();
        if (matches.Length != 1) return null;
        var existing = matches[0];
        return await Prepare(new PreparePaymentRequest(state.ChangeKind, existing.StudentRef, existing.PaymentRef,
            existing.Year, existing.Month, state.Amount ?? existing.Amount, state.Method ?? existing.Method, existing.Date, state.ChangeReason), owner, session);
    }
    private static bool TryReadAmountCorrection(string text, out int amount)
    {
        amount = 0;
        if (!Regex.IsMatch(text, @"\b(perd[oó]n|era(?:n)?|correg[ií]|cambi[aá]|en realidad)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) return false;
        var match = Regex.Match(text, @"\$\s*(\d{1,3}(?:\.\d{3})+|\d{4,8})\b", RegexOptions.CultureInvariant);
        return match.Success && int.TryParse(match.Groups[1].Value.Replace(".", ""), out amount);
    }
    private static string RejectFalseWriteClaim(string message) =>
        Regex.IsMatch(message, @"\b(registr[eé]|cargu[eé]|anul[eé]|correg[ií]|confirm[eé]|qued[oó] registrado|ya est[aá] pago)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            ? "No se registró ningún pago. Para continuar, indicá los datos que faltan o confirmá una propuesta vigente."
            : message;

    private void CapturePaymentDetails(string text, ConversationState state)
    {
        var normalized = text.ToLowerInvariant().Replace('á', 'a').Replace('é', 'e').Replace('í', 'i').Replace('ó', 'o').Replace('ú', 'u');
        var months = new[] { "enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre" };
        for (var index = 0; index < months.Length; index++)
            if (Regex.IsMatch(normalized, @"\b" + months[index] + @"\b", RegexOptions.CultureInvariant)) state.Month = index + 1;
        var year = Regex.Match(normalized, @"\b20\d{2}\b", RegexOptions.CultureInvariant);
        if (year.Success) state.Year = int.Parse(year.Value, CultureInfo.InvariantCulture);
        else if (state.Month == BusinessToday.Month && !state.Year.HasValue) state.Year = BusinessToday.Year;
        var money = Regex.Match(text, @"\$\s*(\d{1,3}(?:\.\d{3})+|\d{4,8})\b", RegexOptions.CultureInvariant);
        if (money.Success && int.TryParse(money.Groups[1].Value.Replace(".", ""), out var amount)) state.Amount = amount;
        if (normalized.Contains("transferencia", StringComparison.Ordinal)) state.Method = "Transferencia";
        else if (normalized.Contains("efectivo", StringComparison.Ordinal)) state.Method = "Efectivo";
        else if (normalized.Contains("beca", StringComparison.Ordinal)) state.Method = "Beca";
    }
    private static string? NormalizeMethod(string? method) => method?.Trim().ToLowerInvariant() switch
    {
        "transferencia" => "Transferencia", "efectivo" => "Efectivo", "beca" => "Beca", _ => null
    };

    private async Task<ChatResponse> AnswerWithLocalData(string intent, ChatRequest request, string owner, string session, string providerMessage)
    {
        var simulated = provider.Name == "mock";
        switch (intent)
        {
            case "students":
                var students = await Students(null, owner, session);
                return new("answer", simulated ? $"Encontré {students.Count} alumnos de prueba. Usá el buscador para elegir uno por nombre, graduación y horario." : providerMessage, provider.Name, Students: students);
            case "payments":
                var payments = await Payments(request.StudentRef, owner, session);
                return new("answer", simulated ? $"Encontré {payments.Count} pagos para el alumno seleccionado. Te muestro el detalle acá." : providerMessage, provider.Name, Payments: payments);
            case "debts":
                var debt = await Debt(request.StudentRef, owner, session);
                var count = debt.PendingMonths.Count;
                return new("answer", simulated ? count == 0 ? "El alumno seleccionado no tiene meses pendientes desde su alta hasta hoy." : $"El alumno seleccionado tiene {count} {(count == 1 ? "mes" : "meses")} sin pago desde su alta hasta hoy. Te muestro cuáles son." : providerMessage, provider.Name, Debt: debt);
            case "statistics":
                var statistics = await Statistics(request.Year!.Value, request.Month!.Value);
                return new("answer", simulated ? $"En {request.Month:00}/{request.Year} hay {statistics.Payments} pagos vigentes. El detalle del período aparece acá." : providerMessage, provider.Name, Statistics: statistics);
            default:
                return new("answer", providerMessage, provider.Name);
        }
    }

    private static string? InferIntent(string text)
    {
        var normalized = text.Trim().ToLowerInvariant()
            .Replace('á', 'a').Replace('é', 'e').Replace('í', 'i').Replace('ó', 'o').Replace('ú', 'u');
        // Una frase libre solo elige consultas de lectura. Las escrituras usan el formulario confirmado.
        if (Regex.IsMatch(normalized, @"\b(registrar|cargar|crear|anular|eliminar|borrar|corregir|editar|modificar|restaurar|revertir)\b")) return null;
        if (Regex.IsMatch(normalized, @"\b(deuda\w*|debe\w*|deben|pendiente\w*|impago\w*)\b") ||
            normalized.Contains("sin pagar") || normalized.Contains("no pago")) return "debts";
        if (Regex.IsMatch(normalized, @"\b(estadistica\w*|recaud\w*|informe\w*|total\w*|resumen\w*)\b")) return "statistics";
        if (Regex.IsMatch(normalized, @"\b(pago\w*|cuota\w*|cobro\w*)\b")) return "payments";
        if (Regex.IsMatch(normalized, @"\b(alumno\w*|estudiante\w*|buscar|listado)\b")) return "students";
        if (Regex.IsMatch(normalized, @"\b(ayuda|puedo|opciones)\b")) return "help";
        return null;
    }

}
