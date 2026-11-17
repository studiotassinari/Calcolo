// Motore importato da TA-MuroDiSostegno: formule conservate per confronto con il programma originale.
// Limiti noti e validazione documentati in docs/MURO-SOSTEGNO.md.
namespace STA.MuroSostegno.Core.Legacy;
// Coordinate metriche dei poligoni del modello originale.
internal struct Punto { public double x, y; }
internal sealed class GeomAnalitica
{
        public bool  CalcolaAreaEBaricentro(Punto[] vertici, out double area, out Punto baricentro)
        {
            // Inizializza i valori di output
            area = 0.0;
            baricentro = new Punto();
            baricentro.x = 0;
            baricentro.y = 0;

            // Verifica che ci siano almeno 3 vertici per formare un poligono
            if (vertici == null || vertici.Length < 3)
            {
                return false;
            }

            int n = vertici.Length;
            double sommaArea = 0.0;
            double sommaCx = 0.0;
            double sommaCy = 0.0;

            // Formula di Gauss per area e baricentro
            // Itera su tutti i lati del poligono
            for (int i = 0; i < n; i++)
            {
                // Indice del vertice successivo (cicla all'inizio quando raggiunge la fine)
                int j = (i + 1) % n;

                // Prodotto vettoriale (cross product) tra vertici consecutivi
                double cross = vertici[i].x * vertici[j].y - vertici[j].x * vertici[i].y;

                // Accumula per il calcolo dell'area
                sommaArea += cross;

                // Accumula per il calcolo del baricentro
                sommaCx += (vertici[i].x + vertici[j].x) * cross;
                sommaCy += (vertici[i].y + vertici[j].y) * cross;
            }

            // Calcola l'area (valore assoluto diviso 2)
            area = Math.Abs(sommaArea) / 2.0;

            // Evita divisione per zero
            if (area == 0.0)
            {
                return false;
            }

            // Calcola le coordinate del baricentro
            // Formula: Cx = (1/6A) * Σ((xi + xi+1) * (xi*yi+1 - xi+1*yi))
            baricentro.x = sommaCx / (6.0 * area);
            baricentro.y = sommaCy / (6.0 * area);

            // Corregge il segno se il poligono è orientato in senso orario
            if (sommaArea < 0)
            {
                baricentro.x = -baricentro.x;
                baricentro.y = -baricentro.y;
            }

            return true;
        }

}
