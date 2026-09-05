-- exec procGrdERPatientVitalsForERPortal 1,1,1
CREATE or alter PROCEDURE [dbo].[procGrdERPatientVitalsForERPortal]
    @intERPatientCode BIGINT = NULL,
    @intBranchCode INT = NULL,
    @intCompanyCode INT = NULL
AS
BEGIN

    SET NOCOUNT ON;

    ;WITH BaseVitals AS
    (
        SELECT
            V.intERPatientVitalsCode AS Code,
            V.intERPatientCode,
            V.dtmVitals AS VitalDate,

            V.intHeight AS [Height (cm)],
            V.numWeight AS [Weight (kg)],
            V.intHeartRate AS HeartRate,
            V.intPulse AS [Pulse (BPM)],
            V.intBP1 AS [BP (Systolic)],
            V.intBP2 AS [BP (Diastolic)],
            V.intResp AS Respiratory,
            V.numTemp AS [Temp (F)],
            V.numGlucoseF AS [Glucose(F)],
            V.numGlucoseR AS [Glucose(R)],
            V.intRecordStatusCode,
            COALESCE(V.intHeartRate, V.intPulse) AS NEWSHeartRate,
            CAST(
                (V.numTemp - 32.0) * 5.0 / 9.0
                AS DECIMAL(5,2)
            ) AS TempCelsius

        FROM tblERpatientVitals V

        WHERE V.intERPatientCode = @intERPatientCode
          AND V.intBranchCode = @intBranchCode
          AND V.intCompanyCode = @intCompanyCode
    ),

    /* =====================================================
       CALCULATE AVAILABLE NEWS2 COMPONENT SCORES
       ===================================================== */
    NEWSComponents AS
    (
        SELECT
            B.*,

            /* =============================================
               RESPIRATORY RATE
               ============================================= */
            CASE
                WHEN B.Respiratory IS NULL THEN NULL

                WHEN B.Respiratory <= 8 THEN 3
                WHEN B.Respiratory BETWEEN 9 AND 11 THEN 1
                WHEN B.Respiratory BETWEEN 12 AND 20 THEN 0
                WHEN B.Respiratory BETWEEN 21 AND 24 THEN 2
                WHEN B.Respiratory >= 25 THEN 3
            END AS NEWS_RespiratoryScore,


            /* =============================================
               SYSTOLIC BLOOD PRESSURE
               ============================================= */
            CASE
                WHEN B.[BP (Systolic)] IS NULL THEN NULL

                WHEN B.[BP (Systolic)] <= 90 THEN 3
                WHEN B.[BP (Systolic)] BETWEEN 91 AND 100 THEN 2
                WHEN B.[BP (Systolic)] BETWEEN 101 AND 110 THEN 1
                WHEN B.[BP (Systolic)] BETWEEN 111 AND 219 THEN 0
                WHEN B.[BP (Systolic)] >= 220 THEN 3
            END AS NEWS_SystolicScore,


            /* =============================================
               HEART RATE / PULSE
               ============================================= */
            CASE
                WHEN B.NEWSHeartRate IS NULL THEN NULL

                WHEN B.NEWSHeartRate <= 40 THEN 3
                WHEN B.NEWSHeartRate BETWEEN 41 AND 50 THEN 1
                WHEN B.NEWSHeartRate BETWEEN 51 AND 90 THEN 0
                WHEN B.NEWSHeartRate BETWEEN 91 AND 110 THEN 1
                WHEN B.NEWSHeartRate BETWEEN 111 AND 130 THEN 2
                WHEN B.NEWSHeartRate >= 131 THEN 3
            END AS NEWS_HeartRateScore,


            /* =============================================
               TEMPERATURE
               NEWS2 uses Celsius
               ============================================= */
            CASE
                WHEN B.TempCelsius IS NULL THEN NULL

                WHEN B.TempCelsius <= 35.0 THEN 3
                WHEN B.TempCelsius <= 36.0 THEN 1
                WHEN B.TempCelsius <= 38.0 THEN 0
                WHEN B.TempCelsius <= 39.0 THEN 1
                WHEN B.TempCelsius > 39.0 THEN 2
            END AS NEWS_TemperatureScore

        FROM BaseVitals B
    ),

    /* =====================================================
       AVAILABLE SCORE + RED PARAMETER
       ===================================================== */
    NEWSCalculated AS
    (
        SELECT
            C.*,

            /*
                IMPORTANT:

                This is NOT yet the complete NEWS2 score.

                It is the minimum/available score calculated
                from:
                    Respiratory
                    Systolic BP
                    Heart Rate
                    Temperature

                SpO2, oxygen and consciousness are still missing.
            */
            COALESCE(C.NEWS_RespiratoryScore, 0)
            + COALESCE(C.NEWS_SystolicScore, 0)
            + COALESCE(C.NEWS_HeartRateScore, 0)
            + COALESCE(C.NEWS_TemperatureScore, 0)
                AS NEWS2AvailableScore,


            /*
                Any available parameter scoring 3
            */
            CASE
                WHEN C.NEWS_RespiratoryScore = 3
                  OR C.NEWS_SystolicScore = 3
                  OR C.NEWS_HeartRateScore = 3
                  OR C.NEWS_TemperatureScore = 3
                THEN 1

                ELSE 0
            END AS HasRedParameter

        FROM NEWSComponents C
    ),

    /* =====================================================
       FIND HIGHEST SCORING PARAMETER
       ===================================================== */
    NEWSFocus AS
    (
        SELECT
            N.*,
            M.MaxComponentScore

        FROM NEWSCalculated N

        OUTER APPLY
        (
            SELECT MAX(X.Score) AS MaxComponentScore
            FROM
            (
                VALUES
                    (N.NEWS_RespiratoryScore),
                    (N.NEWS_SystolicScore),
                    (N.NEWS_HeartRateScore),
                    (N.NEWS_TemperatureScore)
            ) X(Score)
        ) M
    ),

    /* =====================================================
       ADD PREVIOUS SCORE
       ===================================================== */
    NEWSPrevious AS
    (
        SELECT
            N.*,

            LAG(N.NEWS2AvailableScore)
            OVER
            (
                PARTITION BY N.intERPatientCode
                ORDER BY
                    N.VitalDate,
                    N.Code
            ) AS PreviousNEWS2AvailableScore

        FROM NEWSFocus N
    )

    /* =====================================================
       FINAL RESULT
       ===================================================== */
    SELECT

        /* ---------------------------------------------
           ORIGINAL COLUMNS
           --------------------------------------------- */

        N.Code,
        N.VitalDate,

        N.[Height (cm)],
        N.[Weight (kg)],
        N.HeartRate,
        N.[Pulse (BPM)],
        N.[BP (Systolic)],
        N.[BP (Diastolic)],
        N.Respiratory,
        N.[Temp (F)],

        CAST(
            ROUND(N.TempCelsius, 1)
            AS DECIMAL(4,1)
        ) AS [Temp (C)],

        N.[Glucose(F)],
        N.[Glucose(R)],


        /* =============================================
           INDIVIDUAL NEWS2 COMPONENT SCORES
           ============================================= */

        N.NEWS_RespiratoryScore,

        N.NEWS_SystolicScore,

        N.NEWS_HeartRateScore,

        N.NEWS_TemperatureScore,


        /* =============================================
           SCORE CALCULATED FROM AVAILABLE PARAMETERS
           ============================================= */

        N.NEWS2AvailableScore,


        /* =============================================
           FULL NEWS2 SCORE

           NULL intentionally because:
           SpO2
           Oxygen
           Consciousness
           are currently unavailable.
           ============================================= */

        CAST(NULL AS INT) AS NEWS2Score,


        /* =============================================
           DATA STATUS
           ============================================= */

        'INCOMPLETE - SpO2, Oxygen Status and Consciousness/GCS required'
            AS NEWS2DataStatus,


        /* =============================================
           RISK BASED ON MINIMUM/AVAILABLE SCORE

           Since missing components can only ADD to
           the score, an available score >=7 means
           the final NEWS2 will also be >=7.
           ============================================= */

        CASE
            WHEN N.NEWS2AvailableScore >= 7
            THEN 'AT LEAST HIGH'

            WHEN N.NEWS2AvailableScore BETWEEN 5 AND 6
            THEN 'AT LEAST MEDIUM'

            WHEN N.HasRedParameter = 1
            THEN 'RED PARAMETER PRESENT'

            ELSE 'INCOMPLETE'
        END AS NEWS2Risk,


        /* =============================================
           RED FLAG
           ============================================= */

        CASE
            WHEN N.HasRedParameter = 1
                THEN 'YES'

            ELSE 'INCOMPLETE'
        END AS NEWS2RedFlag,


        /* =============================================
           PARAMETER REQUIRING MOST ATTENTION
           ============================================= */

        CASE

            WHEN N.MaxComponentScore IS NULL
            THEN 'No NEWS2 parameters recorded'


            WHEN N.MaxComponentScore = 0
            THEN 'None among available parameters'


            ELSE
                STUFF
                (
                    CASE
                        WHEN N.NEWS_RespiratoryScore =
                             N.MaxComponentScore
                        THEN ', Respiratory Rate'
                        ELSE ''
                    END

                    +

                    CASE
                        WHEN N.NEWS_SystolicScore =
                             N.MaxComponentScore
                        THEN ', Systolic BP'
                        ELSE ''
                    END

                    +

                    CASE
                        WHEN N.NEWS_HeartRateScore =
                             N.MaxComponentScore
                        THEN ', Heart Rate'
                        ELSE ''
                    END

                    +

                    CASE
                        WHEN N.NEWS_TemperatureScore =
                             N.MaxComponentScore
                        THEN ', Temperature'
                        ELSE ''
                    END,

                    1,
                    2,
                    ''
                )

        END AS NEWS2FocusParameter,


        /* =============================================
           PREVIOUS AVAILABLE SCORE
           ============================================= */

        N.PreviousNEWS2AvailableScore,


        /* =============================================
           CHANGE FROM PREVIOUS VITAL
           +ve = worsening
           -ve = improvement
           ============================================= */

        CASE
            WHEN N.PreviousNEWS2AvailableScore IS NULL
            THEN NULL

            ELSE
                N.NEWS2AvailableScore
                - N.PreviousNEWS2AvailableScore

        END AS NEWS2AvailableChange,


        /* =============================================
           TREND
           ============================================= */

        CASE

            WHEN N.PreviousNEWS2AvailableScore IS NULL
                THEN 'First Reading'

            WHEN N.NEWS2AvailableScore >
                 N.PreviousNEWS2AvailableScore
                THEN 'Deteriorating'

            WHEN N.NEWS2AvailableScore <
                 N.PreviousNEWS2AvailableScore
                THEN 'Improving'

            ELSE 'Stable'

        END AS NEWS2Trend,


        /* =============================================
           TRIGGER / ACTION
           ============================================= */

        CASE

            WHEN N.NEWS2AvailableScore >= 7
            THEN
                'Emergency assessment required - complete full NEWS2 immediately'

            WHEN N.NEWS2AvailableScore BETWEEN 5 AND 6
            THEN
                'Urgent clinical assessment - complete full NEWS2 immediately'

            WHEN N.HasRedParameter = 1
            THEN
                'Single red parameter - urgent clinical review'

            ELSE
                'Complete SpO2, Oxygen Status and Consciousness/GCS'

        END AS NEWS2Trigger,


        /* =============================================
           SUGGESTED MONITORING
           ============================================= */

        CASE

            WHEN N.NEWS2AvailableScore >= 7
            THEN
                'Continuous monitoring while completing full NEWS2'

            WHEN N.NEWS2AvailableScore BETWEEN 5 AND 6
            THEN
                'At least hourly monitoring while completing full NEWS2'

            WHEN N.HasRedParameter = 1
            THEN
                'At least hourly monitoring and medical review'

            ELSE
                'Complete missing NEWS2 observations before assigning monitoring frequency'

        END AS SuggestedMonitoring,


        /* =============================================
           SUGGESTED CARE LEVEL

           This is intentionally a suggestion,
           NOT an automatic admission location.
           ============================================= */

        CASE

            WHEN N.NEWS2AvailableScore >= 7
            THEN
                'Critical-care capable monitored area - emergency assessment / consider HDU-ICU'

            WHEN N.NEWS2AvailableScore BETWEEN 5 AND 6
            THEN
                'Monitored acute-care / ER observation area - urgent assessment'

            WHEN N.HasRedParameter = 1
            THEN
                'Monitored area - urgent medical review'

            ELSE
                'Complete full NEWS2 before care-area recommendation'

        END AS SuggestedCareArea,


        N.intRecordStatusCode

    FROM NEWSPrevious N

    ORDER BY
        N.VitalDate DESC,
        N.Code DESC;

END
GO
