using ViewX;

namespace CS_Launcher
{
    /// <summary>
    /// Вспомогательный класс, инкапсулирующий повторные попытки авторизации в ViewX.
    /// </summary>
    class CSLogon
    {
        /// <summary>
        /// HRESULT ошибки общего сбоя.
        /// </summary>
        public const int E_FAIL = unchecked((int)0x80004005);
        /// <summary>
        /// HRESULT ошибки отказа в доступе.
        /// </summary>
        public const int E_ACCESSDENIED = unchecked((int)0x80070005);
        /// <summary>
        /// HRESULT ошибки отсутствия файла.
        /// </summary>
        public const int E_FILENOTFOUND = unchecked((int)0x80070002);
        /// <summary>
        /// HRESULT ошибки некорректного имени.
        /// </summary>
        public const int E_INVALID_NAME = unchecked((int)0x8007007B);
        /// <summary>
        /// HRESULT ошибки неверного аргумента.
        /// </summary>
        public const int E_INVALIDARG = unchecked((int)0x80070057);

        /// <summary>
        /// Выполняет попытку входа в указанный сервер ViewX с заданными учётными данными.
        /// При временных ошибках выполняет несколько повторов.
        /// </summary>
        /// <param name="server_system">Имя или адрес целевой системы.</param>
        /// <param name="user_name">Имя пользователя.</param>
        /// <param name="user_pass">Пароль пользователя.</param>
        /// <param name="ViewXApp">Экземпляр приложения ViewX, через который выполняется логон.</param>
        /// <returns><c>true</c>, если вход выполнен успешно; иначе <c>false</c>.</returns>
        public static bool LogOn(string server_system, string user_name, string user_pass, Application ViewXApp)
        {
            // Итоговый результат по умолчанию — неуспех, пока не будет подтверждён успешный вход.
            bool logon_result = false;

            // Делаем несколько попыток, чтобы сгладить кратковременные сетевые/служебные сбои.
            for (int i = 1; i <= 5; i++)
            {
                try
                {
                    // Основной вызов авторизации в ViewX.
                    ViewXApp.Logon(server_system, user_name, user_pass);
                    logon_result = true;

                    // Небольшая пауза после успешного входа, чтобы дать системе стабилизироваться.
                    Thread.Sleep(1000);
                    break;
                }
                catch (Exception ex)
                {
                    // Если ошибка не связана с отказом в доступе, пробуем повторить попытку.
                    if (ex.HResult != E_ACCESSDENIED)
                    {
                        logon_result = false;
                        Thread.Sleep(1000);
                    }
                    else
                    {
                        // Отказ в доступе повторять бессмысленно: прекращаем попытки сразу.
                        logon_result = false;
                        break;
                    }
                }
            }

            // Возвращаем итог — успешен ли хотя бы один вход.
            return logon_result;
        }
    }
}
