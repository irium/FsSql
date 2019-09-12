if [ ! -e "paket.lock" ]
then
    mono .paket/paket.exe install
fi