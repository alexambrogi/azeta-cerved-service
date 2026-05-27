info: https://learn.microsoft.com/it-it/windows-server/administration/windows-commands/sc-create

Per installare il servizio eseguire


Per eliminare il servizio eseguire
sc delete CervedService

sc create CervedService binpath="C:\Program Files\CERVED_Service\CERVED_Service.exe" DisplayName= "CERVED Service" start=auto