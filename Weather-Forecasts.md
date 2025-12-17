### Weather forecast Assignment

The service will offer users the ability to be updated on weather forecasts for areas
of the territory using multiple forecasting platforms (e.g. Accuweather, EMY,
Meteo.gr). After registering and creating a profile in the system, the user will be able
to search for weather forecast data for specific areas. Forecast items will be current
weather, hourly forecast for the next 3 hours and daily forecast for the next 3 days
with 3 forecasts per day (at 08:00, 15:00 and 21:00).The user will have the ability to
download forecast analytics (the ability to download analytics refers to receiving a
response from the system that includes two or more forecasts, one from each
forecast service) or aggregates resulting from weighting the data of the individual
services ( eg return the forecast with the highest number of user ratings). When
creating their profile, the user will select the forecasting services he wants to use.
The user will be able to rate each prediction on a scale of 1 (not at all satisfied) to 5
(completely satisfied). The users' ratings will be used to calculate the aggregate
forecast per region (depending on the ratings, the system will suggest to the user a
forecast from the service(s) with the highest score).To efficiently respond to
searches, the system will periodically receive and store forecast data from all partner
services. For this purpose, it will provide a suitable API which will be called by the
cooperating services in order to update forecasts by region, when some changes
occur in them. Finally, the application will have the ability to send warning messages
to the user in case of bad weather (only in cases of danger to life) for all the areas of
interest registered in their profile