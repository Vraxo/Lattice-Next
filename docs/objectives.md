[object Object]

The REPL's `:review-price` flow uses the latest bounded page-fetch observation as a source envelope only. The user must select the candidate, amount, currency, and an exact excerpt present in that page, then explicitly accept the displayed source and retrieval time. No amount is extracted or accepted automatically, and fetched text is never treated as an instruction or fact by itself.


## Final plan summaries

When the evaluation produces a shortlisted option with a feasible temporal witness, the runtime adds a short plan summary after the candidate assessments and timetable. It distinguishes details supported by the supplied evidence, assumptions introduced by the evaluator, unresolved candidate/condition/preference evidence, and the user's remaining actions. The summary explicitly warns that live availability, opening hours, transport departures, weather, and booking requirements are not verified by the evaluator; it never implies that a booking was made.
