#!/bin/sh
# Parts of the app (and the RSecurityBackend NuGet package) read appsettings.json directly and ignore env vars
# (connection string, JWT secret/issuer, first admin). Copy those env values into the deployed file before starting.
# Values must not contain double quotes.
set_key() { # set_key <json key> <value>: replace "key": "..." (keys below are unique in appsettings.json)
  [ -n "$2" ] || return 0
  esc=$(printf '%s' "$2" | sed 's/[\\&|]/\\&/g')
  sed -i "s|\"$1\": \"[^\"]*\"|\"$1\": \"$esc\"|" appsettings.json
}
if [ -f appsettings.json ]; then
  set_key DefaultConnection "$ConnectionStrings__DefaultConnection"
  set_key Secret "$Security__Secret"
  set_key ApplicationName "$RSecurityBackend__ApplicationName"
  set_key FirstUserEmail "$RSecurityBackend__FirstUserEmail"
fi
exec dotnet "$APP_DLL"
