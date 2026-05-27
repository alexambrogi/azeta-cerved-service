using Azure.Core;
using IDatabase;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SisterBot.CRUD.Models;
using SisterBot.CRUD.Repository.Abstract;
using SisterBot.CRUD.Repository.Interfaces;
using System;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace CERVED_Service
{
    public class Worker(ILogger<Worker> logger, ISISTERBOTRepository repository) : BackgroundService
    {

        const string Error = "Errore:";

        private enum StatoRichiesta : byte
        {
            DaFare = 0,
            InCorso = 1,
            Fatto = 2,
            Errore = 3
        }

        public enum TipoRicerca : byte
        {
            Presenza_Pregiudizievoli = 12,
            Dettaglio_Pregiudizievoli = 13,
            Nessuna = 99
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("CervedService avvio alle: {time}", DateTimeOffset.Now);

            var executionFolder = AppContext.BaseDirectory;

            // string? Response = await GetCervedData("MBRNTN83H17E506A", TipoRicerca.Presenza_Pregiudizievoli);

            await repository.Commands.ExecuteCommandAsync("UPDATE dbo.RICHIESTE SET STATO = 0, NTENTATIVO=NTENTATIVO-1 WHERE STATO = 1 AND ID_SERVIZIO IN (12, 13)");

            await Task.Delay(5000, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(5000, stoppingToken);

                if (!logger.IsEnabled(LogLevel.Information))
                    continue;

                var resPrimaRicerca = await repository.Commands.GetDataAsync<decimal>("""
                                        SELECT DISTINCT TOP (1) RIC.ID_RICHIESTA, RIC.DATA
                                        FROM dbo.RICHIESTE RIC
                                        INNER JOIN dbo.DATI_RICHIESTE DR ON DR.ID_RICHIESTA = RIC.ID_RICHIESTA
                                        WHERE RIC.STATO IN (0,3) AND RIC.NTENTATIVO < 3 AND DR.STATO IN (0,3) AND RIC.ID_SERVIZIO IN (12, 13)
                                        ORDER BY RIC.DATA
                                        """);

                if (resPrimaRicerca == 0) continue;

                var readItem = await repository.RICHIESTE.GetItemAsync(resPrimaRicerca);

                if (readItem?.Item == null) continue;

                readItem.Item.STATO = (byte)StatoRichiesta.InCorso;
                readItem.Item.INIZIO = DateTime.Now;
                readItem.Item.FINE = null;
                readItem.Item.NTENTATIVO += 1;

                if (!string.IsNullOrEmpty((await repository.RICHIESTE.UpsertAsync(readItem.Item))?.Exception)) return;

                _ = ExecuteSearchAsync(readItem);
            }
        }

        private async Task ExecuteSearchAsync(ReadItemResult<RICHIESTE> readItem)
        {

            if (readItem.Item == null) return;

            logger.LogInformation("Inizio Ricerca per Id_Richiesta: {idRichiesta} alle {data}",
                readItem.Item.ID_RICHIESTA, DateTimeOffset.Now);

            try
            {

                var param = new List<SerializedDbParameter>
                {
                    new("@ID_RICHIESTA", SerializedDbParameter.SldDbType.Decimal) { Value = readItem.Item.ID_RICHIESTA }
                };

                var readRichieste = await repository.DATI_RICHIESTE.GetAsync("ID_RICHIESTA = @ID_RICHIESTA", param);

                if (readRichieste?.Items == null)
                {
                    await UpdateRichiesta(readItem, false, StatoRichiesta.Errore);
                    return;
                }

                foreach (var dato in readRichieste.Items)
                {
                    if (dato.STATO == (int)StatoRichiesta.Fatto)
                        continue;

                    dato.STATO = (int)StatoRichiesta.InCorso;
                    dato.INIZIO = DateTime.Now;

                    dato.NTENTATIVO = (short)((dato.NTENTATIVO < 0) ? 1 : dato.NTENTATIVO + 1);
                    await repository.DATI_RICHIESTE.UpsertAsync(dato);

                    // Faccio comunque la chiamata per avere i flag. 
                    // anche se è da fare il dettaglio, perché potrebbe non avere nessun evento pregiudizievole nè protesti
                    // e quindi non avrebbe senso fare la chiamata dei dettagli che costa anche di più.
                    CV_ANAGRAFICA ana = await ElaboraRichiestaPerFlag(dato);


                    if (readItem.Item.ID_SERVIZIO == (int)TipoRicerca.Dettaglio_Pregiudizievoli &&
                        (ana.HASPREGIUDIZIEVOLI || ana.HASPROTESTI))
                    {
                        for (int nAtt = 0; nAtt < 3; nAtt++)
                        {
                            // Deve fare la chiamata per i dettagli
                            string? Response = await GetCervedData(dato.CF_PIVA, TipoRicerca.Dettaglio_Pregiudizievoli);
                            if (Response != null)
                            {
                                ana.JSONDETAIL = Response;
                                await repository.CV_ANAGRAFICA.UpsertAsync(ana);

                                bool bIsValidResponse = true;
                                // object? theResponse = ElaborateCervedResponse(Response);
                                if (Response.StartsWith(Error))
                                {
                                    bIsValidResponse = false;
                                }

                                if (bIsValidResponse)
                                {
                                    var detailResp = JsonConvert.DeserializeObject<CervedDetailApiResponse>(Response);
                                    // Salvo i dettagli dei protesti
                                    if (dato.CF_PIVA.Length == 16)
                                    {
                                        foreach (var person in detailResp.people)
                                        {
                                            if (person.tax_code.ToUpper() == dato.CF_PIVA.ToUpper())
                                            {
                                                await AddAllPrejudicial(dato, ana, person.prejudicial_events);
                                                await AddAllProtests(dato, ana, person.protests);
                                                nAtt = 5; // Così fermo il ciclo
                                            }
                                        }
                                    }
                                    else // É una partita iva
                                    {
                                        foreach (var company in detailResp.companies)
                                        {
                                            if (company.vat_number.ToUpper() == dato.CF_PIVA.ToUpper())
                                            {
                                                await AddAllPrejudicial(dato, ana, company.prejudicial_events);
                                                await AddAllProtests(dato, ana, company.protests);
                                                nAtt = 5;
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    // Errore da API
                                    ana.ERRORMESSAGE = "Errore in dettagli  " + Response;
                                    await repository.CV_ANAGRAFICA.UpsertAsync(ana);
                                    nAtt = 5;
                                }
                            }
                            else
                            {
                                await Task.Delay(2000); // Attendo 2 secondi prima del nuovo tentativo  
                            }
                        }
                    }
                    else
                    {
                        await repository.CV_ANAGRAFICA.UpsertAsync(ana);
                    }

                    await UpdateDatiRichiesta(dato, false, StatoRichiesta.Fatto, null, DateTime.Now);
                }
                await UpdateRichiesta(readItem, true, StatoRichiesta.Fatto, null, DateTime.Now);
            }
            catch (Exception ex)
            {
                logger.LogError("Errore elaborazione CERVED: {messaggio} per Id_Richiesta: {idRichiesta} alle {data}",
                    ex.Message, readItem.Item.ID_RICHIESTA, DateTimeOffset.Now);
            }
        }

        protected async Task AddAllProtests(DATI_RICHIESTE dato, CV_ANAGRAFICA ana, Protest[] protests)
        {
            if (protests is null)
                return;

            await repository.Commands.ExecuteCommandAsync("DELETE FROM CV_PROTESTI WHERE ID_ANAGRAFICA = @ANA",
                new() { new("@ANA", SerializedDbParameter.SldDbType.Decimal) { Value = ana.ID_ANAGRAFICA } });

            foreach (var protest in protests)
            {
                foreach (var reg in protest.protests_registry)
                {
                    CV_PROTESTI prot = new CV_PROTESTI
                    {
                        ID_ANAGRAFICA = ana.ID_ANAGRAFICA,
                        ID_DATI_RICHIESTE = dato.ID_DATI_RICHIESTE,
                        NOME = protest.personal_data.name,
                        TAX_CODE = protest.personal_data.tax_code,
                        SIGLA_PROV_RESIDENZA = protest.personal_data.residence_province_code,
                        PROV_RESIDENZA = protest.personal_data.residence_province,
                        RESIDENZA_MUNICIPALITA = protest.personal_data.residence_municipality,
                        INDIRIZZO_RESIDENZA = protest.personal_data.residence_address,
                        DATA_NASCITA = protest.personal_data.birth_date,
                        PROV_NASCITA_CODICE = protest.personal_data.birth_province_code,
                        COMUNE_NASCITA = protest.personal_data.birth_municipality,
                        STATO_NASCITA = protest.personal_data.birth_country,
                        LUOGO_NASCITA = protest.personal_data.birth_place,
                        DATA_REGISTRAZIONE = reg.registration_date,
                        IMPORTO = reg.amount,
                        VALUTA = reg.currency,
                        DATA_RACCOLTA = reg.raising_date,
                        COMUNE_PROTESTO = reg.raising_municipality,
                        NUMERO_REGISTRO = reg.register_number,
                        DATA_SCADENZA = reg.expiry_date,
                        TIPO = reg.type,
                        CODICE_TIPO = reg.type_code,
                        RAGIONE_RIFIUTO = reg.refusal_reason,
                        ADDITIONALINFO = reg.additional_info
                    };
                    await repository.CV_PROTESTI.UpsertAsync(prot);
                }
            }
        }

        protected async Task AddAllPrejudicial(DATI_RICHIESTE dato, CV_ANAGRAFICA ana, Prejudicial_Events[] prejudicials)
        {
            if (prejudicials != null && prejudicials.Count() > 0)
            {
                await repository.Commands.ExecuteCommandAsync("DELETE FROM CV_PREGIUDIZIEVOLI WHERE ID_ANAGRAFICA = @ANA",
                    new() { new("@ANA", SerializedDbParameter.SldDbType.Decimal) { Value = ana.ID_ANAGRAFICA } });

                foreach (var pregiudizievole in prejudicials)
                {
                    CV_PREGIUDIZIEVOLI preg = new CV_PREGIUDIZIEVOLI
                    {
                        ID_ANAGRAFICA = ana.ID_ANAGRAFICA,
                        ID_DATI_RICHIESTE = dato.ID_DATI_RICHIESTE,
                        DATA_ATTO = pregiudizievole.deed_date,
                        TIPO_AGENZIA = pregiudizievole.land_agency_type,
                        AGENZIA = pregiudizievole.land_agency,
                        CODICE_COMUNE_AGENZIA = pregiudizievole.land_agency_municipality_belfiore_code,
                        CODICE_TIPO_ATTO = pregiudizievole.deed_type_code,
                        DESCRIZIONE_ATTO = pregiudizievole.deed_description,
                        ATTO_NUM_REG_GENERICO = pregiudizievole.deed_general_registration_number,
                        ATTO_NUM_REG_SPECIFICO = pregiudizievole.deed_specific_registration_number,
                        IMPORTO_ISCRITTO = pregiudizievole.enrolled_amount,
                        CODICE_MINISTERIALE_ATTO = pregiudizievole.deed_ministerial_code,
                        IMPORTO_ISCRITTO_MONETA = pregiudizievole.enrolled_amount_currency_code,
                        IMPORTO_CAPITALE = pregiudizievole.capital_amount,
                        IMPORTO_CAPITALE_MONETA = pregiudizievole.capital_amount_currency_code,
                        CODICE_CERVED = pregiudizievole.cerved_final_code,
                        CLASSE_FINALE_CERVED = pregiudizievole.cerved_final_class,
                        DESCRIZIONE_ATTO_NORM = pregiudizievole.normalized_deed_description
                    };
                    await repository.CV_PREGIUDIZIEVOLI.UpsertAsync(preg);

                    if (pregiudizievole.beneficiary_subjects_personal_data != null)
                    {
                        foreach (var beneficiary in pregiudizievole.beneficiary_subjects_personal_data)
                        {
                            CV_PREGIUDIZIEVOLI_BENEFICIARI cvBeneficiario = new CV_PREGIUDIZIEVOLI_BENEFICIARI
                            {
                                ID_PREGIUDIZIEVOLI = preg.ID_PREGIUDIZIEVOLI,
                                NOME = beneficiary.name,
                                TAX_CODE = beneficiary.tax_code,
                                PROV_NASCITA = beneficiary.birth_province_code,
                                COMUNE_NASCITA = beneficiary.birth_municipality
                            };
                            await repository.CV_PREGIUDIZIEVOLI_BENEFICIARI.UpsertAsync(cvBeneficiario);
                        }
                    }

                    if (pregiudizievole.charged_subjects_personal_data != null)
                    {
                        foreach (var accused in pregiudizievole.charged_subjects_personal_data)
                        {
                            CV_PREGIUDIZIEVOLI_ACCUSATI cvAccusato = new CV_PREGIUDIZIEVOLI_ACCUSATI
                            {
                                ID_PREGIUDIZIEVOLI = preg.ID_PREGIUDIZIEVOLI,
                                NOME = accused.name,
                                TAX_CODE = accused.tax_code,
                                PROV_NASCITA = accused.birth_province_code,
                                COMUNE_NASCITA = accused.birth_municipality,
                                DATA_NASCITA = accused.birth_date
                            };
                            await repository.CV_PREGIUDIZIEVOLI_ACCUSATI.UpsertAsync(cvAccusato);
                        }
                    }

                    if (pregiudizievole.real_estate_details != null)
                    {
                        foreach (var catasto in pregiudizievole.real_estate_details)
                        {
                            CV_CATASTO_PREGIUDIZIEVOLI cvCatasto = new CV_CATASTO_PREGIUDIZIEVOLI
                            {
                                ID_PREGIUDIZIEVOLI = preg.ID_PREGIUDIZIEVOLI,
                                COMUNE = catasto.municipality,
                                CODICE_COMUNE = catasto.municipality_code,
                                // INDIRIZZO = catasto.address,
                                FOGLIO = catasto.cadastral_sheet,
                                MAPPALE = catasto.cadastral_map_number,
                                SUB = catasto.cadastral_sub_number,
                                TIPO_CATEGORIA = catasto.cadastral_category_type,
                                CATEGORIA = catasto.cadastral_category
                            };
                            await repository.CV_CATASTO_PREGIUDIZIEVOLI.UpsertAsync(cvCatasto);
                        }
                    }
                }
            }
        }


        /// <summary>
        /// Ritorna true se ha protesti o pregiudizievoli, false altrimenti
        /// </summary>
        /// <param name="dato"></param>
        /// <returns></returns>
        private async Task<CV_ANAGRAFICA> ElaboraRichiestaPerFlag(DATI_RICHIESTE dato)
        {
            var toRet = new CV_ANAGRAFICA
            {
                ID_DATI_RICHIESTE = dato.ID_DATI_RICHIESTE,
                CFPIVA = dato.CF_PIVA,
                HASPROTESTI = false,
                HASPREGIUDIZIEVOLI = false,
                DATA_ESECUZIONE = DateTime.Now
            };

            // Provo a vedere se è già stata fatta questo dati_richieste
            var readAna = await repository.CV_ANAGRAFICA.GetAsync("ID_DATI_RICHIESTE = @ID_DATI_RICHIESTE AND CFPIVA = @CF_PIVA",
                new List<SerializedDbParameter>
                {
                    new("@ID_DATI_RICHIESTE", SerializedDbParameter.SldDbType.Decimal) { Value = dato.ID_DATI_RICHIESTE },
                    new("@CF_PIVA", SerializedDbParameter.SldDbType.String) { Value = dato.CF_PIVA }
                });

            if (readAna != null && readAna.Items != null && readAna.Items.Count() > 0)
            {
                toRet = readAna.Items.First();
                toRet.DATA_ESECUZIONE = DateTime.Now;
            }

            for (; dato.NTENTATIVO < 3; dato.NTENTATIVO++)
            {
                try
                {

                    string? Response = await GetCervedData(dato.CF_PIVA, TipoRicerca.Presenza_Pregiudizievoli);

                    if (Response != null)
                    {
                        bool bIsValidResponse = true;
                        // object? theResponse = ElaborateCervedResponse(Response);
                        if (Response.StartsWith(Error))
                        {
                            bIsValidResponse = false;
                        }

                        toRet.JSONFLAGS = Response;

                        if (bIsValidResponse)
                        {
                            var flagsResp = JsonConvert.DeserializeObject<CervedFlagsResponse>(Response);

                            if (dato.CF_PIVA.Length == 16)
                            {
                                // É un codice fiscale
                                if (flagsResp.people?.Count() > 0)
                                {
                                    foreach (var person in flagsResp.people)
                                    {
                                        if (person.taxCode.ToUpper() == dato.CF_PIVA.ToUpper())
                                        {
                                            // Aggiorno la richiesta come fatta
                                            toRet.HASPROTESTI = person.protests;
                                            toRet.HASPREGIUDIZIEVOLI = person.prejudicialEvents;
                                            return toRet;
                                        }
                                    }
                                }
                                toRet.ERRORMESSAGE = "Codice Fiscale non trovato";
                                return toRet; // Non trovato
                            }
                            else //É una partita iva
                            {
                                if (flagsResp.companies?.Count() > 0)
                                {
                                    foreach (var company in flagsResp.companies)
                                    {
                                        if (company.taxCode.ToUpper() == dato.CF_PIVA.ToUpper())
                                        {
                                            toRet.HASPROTESTI = company.protests;
                                            toRet.HASPREGIUDIZIEVOLI = company.prejudicialEvents;
                                            return toRet;
                                        }
                                    }
                                }
                                toRet.ERRORMESSAGE = "Partita IVA non trovata";
                                return toRet; // Non trovato
                            }
                        }
                        else
                        {
                            // Errore da API
                            toRet.ERRORMESSAGE = Response;
                            return toRet;
                        }
                    }
                    else
                    {
                        await Task.Delay(2000); // Attendo 2 secondi prima del nuovo tentativo  
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError("Errore elaborazione CERVED: {messaggio} per CF/PIVA: {cfPiva} alle {data}",
                        ex.Message, dato.CF_PIVA, DateTimeOffset.Now);
                    await Task.Delay(2000); // Attendo 2 secondi prima del nuovo tentativo
                }
            }
            return null;
        }

        private object? ElaborateCervedResponse(string strResponse)
        {
            if (string.IsNullOrEmpty(strResponse)) return null;

            try
            {
                var obj = JsonConvert.DeserializeObject<dynamic>(strResponse);

                if (obj?.status <= 0 || obj?.status > 0) // Solo per vedere se ha il campo status. Così so che è risposta di errore
                {
                    return new CervedErrorApiResponse
                    {
                        status = obj.status,
                        serviceError = obj.serviceError,
                        statusDescription = obj.statusDescription,
                        serviceErrorDescription = obj.serviceErrorDescription
                    };
                }

                // Prendo il campo protests per capire se è un flag (boolean) o un dettaglio (array)
                var protests = obj?.companies[0]?.protests ?? obj?.people[0]?.protests;

                if (protests == null)
                    return null;

                if (protests.Type == Newtonsoft.Json.Linq.JTokenType.Boolean) // Flags
                {
                    return JsonConvert.DeserializeObject<CervedFlagsResponse>(strResponse);
                }

                else if (protests.Type == Newtonsoft.Json.Linq.JTokenType.Array)
                {
                    return JsonConvert.DeserializeObject<CervedDetailApiResponse>(strResponse);
                }

            }
            catch (Exception ex)
            {

            }

            return null;
        }

        private async Task<string?> GetCervedData(string cfPiva, TipoRicerca tipo)
        {
            if (tipo == TipoRicerca.Nessuna)
                return null;

#if DEBUG
            //if (tipo == TipoRicerca.Presenza_Pregiudizievoli)
            //{
            //    return """
            //                    {
            //      "companies": [
            //        {
            //          "subjectId": 366277521,
            //          "taxCode": "TRMLGU70C23A479H",
            //          "protests": false,
            //          "prejudicialEvents": false,
            //          "procedures": false,
            //          "crisisEvents": false,
            //          "cigs": false,
            //          "personalBankruptcies": false
            //        },
            //        {
            //          "subjectId": 361245523,
            //          "taxCode": "TRMLGU70C23A479H",
            //          "name": "IL BUON GUSTO DI TROMBETTA LUIGI",
            //          "vatNumber": "01523170056",
            //          "protests": false,
            //          "prejudicialEvents": false,
            //          "procedures": false,
            //          "crisisEvents": false,
            //          "cigs": false,
            //          "personalBankruptcies": false
            //        }
            //      ],
            //      "people": [
            //        {
            //          "subjectId": 330822204,
            //          "taxCode": "TRMLGU70C23A479H",
            //          "protests": true,
            //          "prejudicialEvents": true,
            //          "procedures": false,
            //          "crisisEvents": false,
            //          "cigs": false,
            //          "personalBankruptcies": false
            //        }
            //      ]
            //    }
            //    """;
            //}
            //else if (tipo == TipoRicerca.Dettaglio_Pregiudizievoli)
            //{
            //    return """
            //                            {
            //          "companies": [
            //            {
            //              "subject_id": 366277521,
            //              "tax_code": "TRMLGU70C23A479H",
            //              "name": "TROMBETTA LUIGI"
            //            },
            //            {
            //              "subject_id": 361245523,
            //              "tax_code": "TRMLGU70C23A479H",
            //              "vat_number": "01523170056",
            //              "name": "IL BUON GUSTO DI TROMBETTA LUIGI"
            //            }
            //          ],
            //          "people": [
            //            {
            //              "subject_id": 330822204,
            //              "tax_code": "TRMLGU70C23A479H",
            //              "name": "TROMBETTA LUIGI",
            //              "protests": [
            //                {
            //                  "personal_data": {
            //                    "name": "TROMBETTA LUIGI",
            //                    "tax_code": "TRMLGU70C23A479H",
            //                    "residence_province_code": "AT",
            //                    "residence_province": "ASTI",
            //                    "residence_municipality": "AT014",
            //                    "residence_address": "VIA MAZZINI 52"
            //                  },
            //                  "protests_registry": [
            //                    {
            //                      "registration_date": "16-05-2023",
            //                      "amount": 103,
            //                      "currency": "EURO",
            //                      "raising_date": "18-04-2023",
            //                      "raising_municipality": "ASTI",
            //                      "register_number": "3473",
            //                      "expiry_date": "15-04-2023",
            //                      "type": "CAMBIALE",
            //                      "type_code": "C",
            //                      "refusal_reason": "Il domiciliatario non paga per mancanza di istruzioni"
            //                    }
            //                  ]
            //                },
            //                {
            //                  "personal_data": {
            //                    "name": "TROMBETTA LUIGI",
            //                    "tax_code": "TRMLGU70C23A479H",
            //                    "residence_province_code": "AT",
            //                    "residence_province": "ASTI",
            //                    "residence_municipality": "AT014",
            //                    "residence_address": "VIA MAZZINI"
            //                  },
            //                  "protests_registry": [
            //                    {
            //                      "registration_date": "15-06-2023",
            //                      "amount": 103,
            //                      "currency": "EURO",
            //                      "raising_date": "16-05-2023",
            //                      "raising_municipality": "CANELLI",
            //                      "register_number": "886",
            //                      "expiry_date": "15-05-2023",
            //                      "type": "CAMBIALE",
            //                      "type_code": "C",
            //                      "refusal_reason": "Il domiciliatario non paga per mancanza di istruzioni"
            //                    }
            //                  ]
            //                }
            //              ],
            //              "prejudicial_events": [
            //                {
            //                  "deed_date": "29-04-2024",
            //                  "land_agency_type": "S",
            //                  "land_agency": "CASALE MONFERRATO",
            //                  "land_agency_province_code": "AL",
            //                  "land_agency_municipality": "CALLIANO MONFERRATO",
            //                  "land_agency_municipality_belfiore_code": "B418",
            //                  "deed_type_code": "TR",
            //                  "deed_description": "VERBALE DI PIGNORAMENTO IMMOBI",
            //                  "deed_general_registration_number": 2189,
            //                  "deed_specific_registration_number": 1862,
            //                  "enrolled_amount": 0,
            //                  "enrolled_amount_currency_code": "EUR",
            //                  "capital_amount": 0,
            //                  "capital_amount_currency_code": "EUR",
            //                  "deed_ministerial_code": 726,
            //                  "cerved_final_code": 1511,
            //                  "cerved_final_class": "A",
            //                  "normalized_deed_description": "ATTI ESECUTIVI O CAUTELARI - VERBALE DI PIGNORAMENTO IMMOBILI",
            //                  "charged_subjects_personal_data": [
            //                    {
            //                      "name": "TROMBETTA LUIGI",
            //                      "tax_code": "TRMLGU70C23A479H",
            //                      "birth_province_code": "AT",
            //                      "birth_municipality": "ASTI",
            //                      "birth_date": "23-03-1970"
            //                    }
            //                  ],
            //                  "beneficiary_subjects_personal_data": [
            //                    {
            //                      "name": "CASSA DI RISPARMIO DI ASTI S.P.A.",
            //                      "tax_code": "00060550050",
            //                      "birth_province_code": "AT",
            //                      "birth_municipality": "ASTI"
            //                    }
            //                  ]
            //                },
            //                {
            //                  "deed_date": "16-11-2023",
            //                  "land_agency_type": "S",
            //                  "land_agency": "CASALE MONFERRATO",
            //                  "land_agency_province_code": "AL",
            //                  "land_agency_municipality": "CALLIANO MONFERRATO",
            //                  "land_agency_municipality_belfiore_code": "B418",
            //                  "deed_type_code": "TR",
            //                  "deed_description": "VERBALE DI PIGNORAMENTO IMMOBI",
            //                  "deed_general_registration_number": 5740,
            //                  "deed_specific_registration_number": 4741,
            //                  "enrolled_amount": 0,
            //                  "enrolled_amount_currency_code": "EUR",
            //                  "capital_amount": 0,
            //                  "capital_amount_currency_code": "EUR",
            //                  "deed_ministerial_code": 726,
            //                  "cerved_final_code": 1511,
            //                  "cerved_final_class": "A",
            //                  "normalized_deed_description": "ATTI ESECUTIVI O CAUTELARI - VERBALE DI PIGNORAMENTO IMMOBILI",
            //                  "charged_subjects_personal_data": [
            //                    {
            //                      "name": "TROMBETTA LUIGI",
            //                      "tax_code": "TRMLGU70C23A479H",
            //                      "birth_province_code": "AT",
            //                      "birth_municipality": "ASTI",
            //                      "birth_date": "23-03-1979"
            //                    }
            //                  ],
            //                  "beneficiary_subjects_personal_data": [
            //                    {
            //                      "name": "CASSADI RISPARMIO DI ASTI S.P.A.",
            //                      "tax_code": "00060550050",
            //                      "birth_province_code": "AT",
            //                      "birth_municipality": "ASTI"
            //                    }
            //                  ],
            //                  "real_estate_details": [
            //                    {
            //                      "municipality": "CALLIANO",
            //                      "municipality_code": "B418",
            //                      "cadastral_sheet": "11",
            //                      "cadastral_map_number": "1043",
            //                      "cadastral_category_type": "ENTE URBANO",
            //                      "cadastral_category": "EU"
            //                    },
            //                    {
            //                      "municipality": "CALLIANO",
            //                      "municipality_code": "B418",
            //                      "cadastral_sheet": "11",
            //                      "cadastral_map_number": "1043",
            //                      "cadastral_sub_number": "1",
            //                      "cadastral_category_type": "ABITAZIONI DI TIPO POPOLARE",
            //                      "cadastral_category": "A4"
            //                    },
            //                    {
            //                      "municipality": "CALLIANO",
            //                      "municipality_code": "B418",
            //                      "cadastral_sheet": "11",
            //                      "cadastral_map_number": "1043",
            //                      "cadastral_sub_number": "2",
            //                      "cadastral_category_type": "ABITAZIONI DI TIPO POPOLARE",
            //                      "cadastral_category": "A4"
            //                    }
            //                  ]
            //                }
            //              ]
            //            }
            //          ]
            //        }
            //        """;
            //}
#endif

            string urlEvents = "https://api.cerved.com/cervedApi/v1.1/rischi/negative/events/flags/ALL?taxCode=" + cfPiva;
            string urlDetails = "https://api.cerved.com/cervedApi/v1/risks/negative/events?tax_code=" + cfPiva;

            string url = string.Empty;

            if (tipo == TipoRicerca.Presenza_Pregiudizievoli)
                url = urlEvents;
            else if (tipo == TipoRicerca.Dettaglio_Pregiudizievoli)
                url = urlDetails;

            if (string.IsNullOrEmpty(url))
                return null;

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    // Add API key to the request header
                    client.DefaultRequestHeaders.Add("apikey", Common.API);

                    HttpResponseMessage response = await client.GetAsync(url);

                    if (response.IsSuccessStatusCode)
                    {
                        string responseBody = await response.Content.ReadAsStringAsync();
                        Console.WriteLine(responseBody);
                        return responseBody;
                    }
                    else
                    {
                        Console.WriteLine($"Error: {response.StatusCode}");
                        return $"{Error} {response.ReasonPhrase}";
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError("Errore interrogazione CERVED: {messaggio} per CF/PIVA: {cfPiva} alle {data}",
                    ex.Message, cfPiva, DateTimeOffset.Now);
            }

            return null;
        }

        private async Task UpdateRichiesta(ReadItemResult<RICHIESTE> readItem, bool addTentativo,
                                           StatoRichiesta? stato = null, DateTime? start = null, DateTime? end = null)
        {
            if (readItem.Item == null) return;
            if (stato != null) readItem.Item.STATO = (byte)stato;
            if (start != null) readItem.Item.INIZIO = start;
            if (end != null) readItem.Item.FINE = end;
            if (addTentativo) readItem.Item.NTENTATIVO += 1;

            await repository.RICHIESTE.UpsertAsync(readItem.Item);
        }

        private async Task UpdateDatiRichiesta(DATI_RICHIESTE readItem, bool addTentativo,
                                               StatoRichiesta? stato = null, DateTime? start = null, DateTime? end = null)
        {
            if (stato != null) readItem.STATO = (byte)stato;
            if (start != null) readItem.INIZIO = start;
            if (end != null) readItem.FINE = end;
            if (addTentativo) readItem.NTENTATIVO += 1;

            await repository.DATI_RICHIESTE.UpsertAsync(readItem);
        }
    }
}
