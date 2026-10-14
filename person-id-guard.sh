#!/bin/bash
# Written by Muse Spark 1.3 AI.
# All appUser.PersonId reads must go through ValidatePersonIdAndReturn().
# Fails the build otherwise. Allowed: the entity itself, EF configuration, tests.
if grep -rn "\.PersonId" src --include='*.cs' \
  | grep -v obj | grep -v bin \
  | grep -v "Configurations/" \
  | grep -v "Migrations/" \
  | grep -v "PersonIdMin" \
  | grep -v "ValidatePersonIdAndReturn()" \
  | grep -v "int? PersonId" \
  | grep -vE "PersonId (==|!=) null|PersonId is (not )?null"; then
  echo "DIRECT PersonId READ FOUND - use ValidatePersonIdAndReturn()"
  exit 1
fi
echo "PersonId guard clean."
