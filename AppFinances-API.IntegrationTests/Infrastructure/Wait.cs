namespace AppFinances_API.IntegrationTests.Infrastructure
{
    public static class Wait
    {
        public static async Task<T> UntilAsync<T>(Func<Task<T>> read, Func<T, bool> done, string what, int timeoutSeconds = 60)
        {
            var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
            T last;

            do
            {
                last = await read();
                if (done(last)) return last;
                await Task.Delay(250);
            } while (DateTime.UtcNow < deadline);

            throw new TimeoutException($"Esperou {timeoutSeconds}s por {what}. Último valor: {last}");
        }
    }
}
