using System;
namespace Calcestruzzo;

/// <summary>
/// Proprietà progettuali NTC 2018 per calcestruzzo di peso normale, a 28 giorni.
/// Resistenze e modulo in MPa. Non esegue la qualificazione della miscela né i controlli di accettazione.
/// </summary>
public sealed class ConcreteNtc2018
{
    public double Rck { get; }
    public double Fck => .83 * Rck;
    public double Fcm => Fck + 8;
    public double Fcd => ElementFactor * .85 * Fck / 1.5;
    public double Fctm { get; }
    public double Fctk05 => .7 * Fctm;
    public double Fctk95 => 1.3 * Fctm;
    public double Fctd => ElementFactor * Fctk05 / 1.5;
    public double Fcfm => 1.2 * Fctm;
    public double Ecm { get; }
    public double ElementFactor { get; }

    /// <summary>
    /// Il riciclo presuppone miscela qualificata nei limiti della Tab. 11.2.III.
    /// Il modulo sperimentale è già quello effettivo del materiale: non viene ridotto una seconda volta.
    /// </summary>
    public ConcreteNtc2018(ClassiNormale classe, bool recycled = false, bool steam = false,
        double? measuredEcm = null, bool thinCastInPlace = false)
    {
        if (!Enum.IsDefined(classe) || classe == ClassiNormale.C100_115)
            throw new ArgumentException("Classe fuori dal catalogo NTC 2018 implementato (Tab. 4.1.I). C100/115 richiede una valutazione specifica.");
        Rck = new Calcestruzzo(classe).Rck;
        ElementFactor = thinCastInPlace ? .8 : 1;
        // §§11.2.10.1–2: conversione cubica/cilindrica e due rami della resistenza a trazione.
        Fctm = (Rck <= 60 ? .3 * Math.Pow(Fck, 2.0 / 3.0) : 2.12 * Math.Log(1 + Fcm / 10))
            * (recycled ? .9 : 1);
        if (measuredEcm.HasValue && (!double.IsFinite(measuredEcm.Value) || measuredEcm.Value <= 0))
            throw new ArgumentException("Ecm sperimentale deve essere finito e positivo, in MPa.");
        if (steam && !measuredEcm.HasValue)
            throw new ArgumentException("Maturazione a vapore: la formula [11.2.5] non è applicabile. Inserire Ecm da prove.");
        // §11.2.10.3: riduzione del 20% per riciclo solo quando si usa la stima teorica.
        Ecm = measuredEcm ?? 22000 * Math.Pow(Fcm / 10, .3) * (recycled ? .8 : 1);
    }
}
