using AppFinancesClassLibrary.Domain.Exceptions;
using Npgsql;
using System.Net;
using System.Text.Json;

namespace AppFinances_API.Middleware
{
    public class DomainExceptionMiddleware
    {
        private readonly RequestDelegate _next;

        public DomainExceptionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (UserNotFoundException)
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = "Usuário não encontrado." }));
            }
            catch (NotFoundException ex)
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = ex.Message }));
            }
            catch (ForbiddenException ex)
            {
                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = ex.Message }));
            }
            catch (UnauthorizedException ex)
            {
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = ex.Message }));
            }
            catch (DomainValidationException ex)
            {
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = ex.Message }));
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                context.Response.StatusCode = (int)HttpStatusCode.Conflict;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = UniqueViolationMessage(ex.ConstraintName) }));
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
            {
                var isDelete = HttpMethods.IsDelete(context.Request.Method);

                context.Response.StatusCode = (int)(isDelete ? HttpStatusCode.Conflict : HttpStatusCode.BadRequest);
                context.Response.ContentType = "application/json";
                var message = isDelete
                    ? ForeignKeyDeleteMessage(ex.ConstraintName)
                    : "O registro informado não existe.";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = message }));
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.CheckViolation)
            {
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = CheckViolationMessage(ex.ConstraintName) }));
            }
        }

        private static string CheckViolationMessage(string? constraintName) => constraintName switch
        {
            "transactions_payment_value_cents_check" => "O valor da transação precisa ser maior que zero.",
            "transactions_payment_value_cents_positive_check" => "O valor da transação precisa ser maior que zero.",
            "transactions_meta_id_direction_check" => "MetaDirection é obrigatório quando MetaId está preenchido, e vice-versa.",
            _ => "Dado inválido."
        };

        private static string ForeignKeyDeleteMessage(string? constraintName) => constraintName switch
        {
            "transactions_category_id_fkey" => "Esta categoria possui transações e não pode ser excluída.",
            "behavioral_insights_category_id_fkey" => "Esta categoria possui insights vinculados e não pode ser excluída.",
            "cards_bank_account_id_fkey" => "Esta conta possui cartões vinculados e não pode ser excluída.",
            "transactions_bank_account_id_fkey" => "Esta conta possui transações e não pode ser excluída.",
            "bank_statements_card_id_fkey" => "Este cartão possui faturas e não pode ser excluído.",
            "transactions_card_id_fkey" => "Este cartão possui transações e não pode ser excluído.",
            "transactions_bank_statement_id_fkey" => "Esta fatura possui transações e não pode ser excluída.",
            "transactions_meta_id_fkey" => "Esta meta possui transações e não pode ser excluída.",
            "documents_transaction_id_fkey" => "Esta transação possui comprovantes vinculados e não pode ser excluída.",
            _ => "Este registro possui dados vinculados e não pode ser excluído."
        };

        private static string UniqueViolationMessage(string? constraintName) => constraintName switch
        {
            "ux_users_email_active" => "Já existe um usuário cadastrado com este e-mail.",
            "ux_users_phone_active" => "Já existe um usuário cadastrado com este telefone.",
            "ux_authentication_username_active" => "Este nome de usuário já está em uso.",
            "authentication_user_id_key" => "Este usuário já possui credenciais cadastradas.",
            "bank_statements_card_id_month_reference_key" => "Já existe uma fatura deste cartão para este mês.",
            "device_tokens_user_id_token_key" => "Este dispositivo já está registrado para o usuário.",
            "weekly_profile_metrics_user_id_week_reference_key" => "Já existem métricas deste usuário para esta semana.",
            "ux_categories_one_default_per_user" => "Este usuário já possui a categoria Metas.",
            _ => "Registro duplicado."
        };
    }
}
